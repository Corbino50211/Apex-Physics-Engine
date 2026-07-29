using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Runtime state for one editor-generated destructible chunk.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexDestructibleChunk : MonoBehaviour
    {
        [SerializeField, HideInInspector] private ApexDestructible owner;
        [SerializeField, HideInInspector] private Rigidbody chunkBody;
        [SerializeField, HideInInspector] private Renderer[] chunkRenderers = System.Array.Empty<Renderer>();
        [SerializeField, HideInInspector] private Collider[] chunkColliders = System.Array.Empty<Collider>();

        private bool released;

        public ApexDestructible Owner => owner;
        public Rigidbody Rigidbody => chunkBody;
        public bool IsReleased => released;
        public Vector3 WorldCenter => chunkBody != null
            ? chunkBody.worldCenterOfMass
            : transform.position;

        private void Reset()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnCollisionEnter(Collision collision)
        {
            owner?.HandleChunkCollision(this, collision);
        }

        public void Configure(ApexDestructible destructible)
        {
            owner = destructible;
            CacheComponents();
        }

        public void PrepareHidden()
        {
            CacheComponents();
            released = false;

            for (int i = 0; i < chunkRenderers.Length; i++)
            {
                if (chunkRenderers[i] != null)
                {
                    chunkRenderers[i].enabled = false;
                }
            }

            for (int i = 0; i < chunkColliders.Length; i++)
            {
                if (chunkColliders[i] != null)
                {
                    chunkColliders[i].enabled = false;
                }
            }

            if (chunkBody != null)
            {
                chunkBody.isKinematic = true;
                chunkBody.detectCollisions = false;
                chunkBody.velocity = Vector3.zero;
                chunkBody.angularVelocity = Vector3.zero;
            }
        }

        public void RevealLocked()
        {
            CacheComponents();

            for (int i = 0; i < chunkRenderers.Length; i++)
            {
                if (chunkRenderers[i] != null)
                {
                    chunkRenderers[i].enabled = true;
                }
            }

            for (int i = 0; i < chunkColliders.Length; i++)
            {
                if (chunkColliders[i] != null)
                {
                    chunkColliders[i].enabled = true;
                }
            }

            if (chunkBody != null && !released)
            {
                chunkBody.isKinematic = true;
                chunkBody.detectCollisions = true;
            }
        }

        public void Release(
            Vector3 inheritedVelocity,
            Vector3 inheritedAngularVelocity,
            Vector3 outwardImpulse,
            Vector3 impactPoint,
            float lifetime)
        {
            if (released)
            {
                return;
            }

            RevealLocked();
            released = true;

            if (chunkBody != null)
            {
                chunkBody.isKinematic = false;
                chunkBody.detectCollisions = true;
                chunkBody.velocity = inheritedVelocity;
                chunkBody.angularVelocity = inheritedAngularVelocity;
                chunkBody.WakeUp();

                if (outwardImpulse.sqrMagnitude > 0.000001f)
                {
                    chunkBody.AddForceAtPosition(
                        outwardImpulse,
                        impactPoint,
                        ForceMode.Impulse);
                }
            }

            owner?.NotifyChunkReleased(this);

            if (lifetime > 0f)
            {
                Destroy(gameObject, lifetime);
            }
        }

        public bool IsCollisionFromSameDestructible(Collision collision)
        {
            if (collision == null || owner == null)
            {
                return false;
            }

            ApexDestructibleChunk otherChunk =
                collision.collider != null
                    ? collision.collider.GetComponentInParent<ApexDestructibleChunk>()
                    : null;
            return otherChunk != null && otherChunk.owner == owner;
        }

        private void CacheComponents()
        {
            if (chunkBody == null)
            {
                chunkBody = GetComponent<Rigidbody>();
            }

            if (chunkRenderers == null || chunkRenderers.Length == 0)
            {
                chunkRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (chunkColliders == null || chunkColliders.Length == 0)
            {
                chunkColliders = GetComponentsInChildren<Collider>(true);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigureGeneratedChunk(ApexDestructible destructible)
        {
            owner = destructible;
            chunkBody = GetComponent<Rigidbody>();
            chunkRenderers = GetComponentsInChildren<Renderer>(true);
            chunkColliders = GetComponentsInChildren<Collider>(true);
        }
#endif
    }
}

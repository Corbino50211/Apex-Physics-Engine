using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PancakeDevs.ApexPhysics
{
    public enum ApexFractureTrigger
    {
        ManualOnly = 0,
        ImpactThreshold = 1
    }

    public enum ApexImpactThresholdMeasurement
    {
        CollisionImpulse = 0,
        EstimatedForce = 1
    }

    /// <summary>
    /// Swaps an intact object to editor-generated fracture chunks. Runtime activation can
    /// be manual-only or automatic when a collision exceeds a configured threshold.
    /// Nearby chunks are released first while remaining visible chunks stay kinematic
    /// until later impacts reach them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexDestructible : MonoBehaviour
    {
        [Header("Runtime Fracture Trigger")]
        [SerializeField] private ApexFractureTrigger fractureTrigger =
            ApexFractureTrigger.ImpactThreshold;
        [SerializeField] private ApexImpactThresholdMeasurement thresholdMeasurement =
            ApexImpactThresholdMeasurement.CollisionImpulse;
        [FormerlySerializedAs("breakImpulse")]
        [SerializeField, Min(0f)] private float breakThreshold = 8f;

        [Header("Break Settings")]
        [SerializeField, Min(0.01f)] private float impactRadius = 0.8f;
        [SerializeField] private bool breakAllAtOnce;
        [SerializeField, Min(0f)] private float secondaryBreakImpulse = 3f;

        [Header("Momentum")]
        [SerializeField] private bool preserveMomentum = true;
        [SerializeField, Min(0f)] private float outwardImpulseMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float inheritedAngularVelocityMultiplier = 1f;

        [Header("Debris")]
        [SerializeField] private bool destroyReleasedChunks;
        [SerializeField, Min(0.1f)] private float debrisLifetime = 20f;

        [SerializeField, HideInInspector] private ApexBody apexBody;
        [SerializeField, HideInInspector] private Transform generatedChunksRoot;
        [SerializeField, HideInInspector] private ApexDestructibleChunk[] chunks =
            Array.Empty<ApexDestructibleChunk>();
        [SerializeField, HideInInspector] private Renderer[] intactRenderers =
            Array.Empty<Renderer>();
        [SerializeField, HideInInspector] private Collider[] intactColliders =
            Array.Empty<Collider>();
        [SerializeField, HideInInspector] private string generatedAssetFolder = string.Empty;

        private Rigidbody sourceBody;
        private bool fractured;
        private Vector3 sourceLinearVelocity;
        private Vector3 sourceAngularVelocity;
        private Vector3 sourceCenterOfMass;

        public event Action<ApexDestructible> Fractured;
        public event Action<ApexDestructibleChunk> ChunkReleased;

        public ApexFractureTrigger FractureTrigger => fractureTrigger;
        public ApexImpactThresholdMeasurement ThresholdMeasurement => thresholdMeasurement;
        public float BreakThreshold => breakThreshold;
        public float BreakImpulse => breakThreshold;
        public bool IsFractured => fractured;
        public bool HasGeneratedFracture =>
            generatedChunksRoot != null && chunks != null && chunks.Length > 0;
        public int ChunkCount => chunks != null ? chunks.Length : 0;
        public int ReleasedChunkCount
        {
            get
            {
                if (chunks == null)
                {
                    return 0;
                }

                int count = 0;
                for (int i = 0; i < chunks.Length; i++)
                {
                    if (chunks[i] != null && chunks[i].IsReleased)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public float ImpactRadius => impactRadius;
        public Transform GeneratedChunksRoot => generatedChunksRoot;
        public string GeneratedAssetFolder => generatedAssetFolder;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            PrepareGeneratedChunks();
        }

        private void OnEnable()
        {
            CacheReferences();
            if (apexBody != null)
            {
                apexBody.Impacted += HandleApexImpact;
            }
        }

        private void OnDisable()
        {
            if (apexBody != null)
            {
                apexBody.Impacted -= HandleApexImpact;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (apexBody != null || fractured || collision == null ||
                fractureTrigger != ApexFractureTrigger.ImpactThreshold)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            if (!ThresholdReached(impulse))
            {
                return;
            }

            Vector3 point = transform.position;
            Vector3 normal = Vector3.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            BreakAt(point, -normal, impulse);
        }

        /// <summary>
        /// Breaks the object near a world-space point regardless of the configured runtime
        /// trigger. The impulse direction should point from the impact into the object.
        /// </summary>
        public void BreakAt(
            Vector3 worldPoint,
            Vector3 impulseDirection,
            float impulseMagnitude)
        {
            if (!HasGeneratedFracture)
            {
                Debug.LogWarning(
                    $"{name} cannot break because no Apex fracture chunks have been generated.",
                    this);
                return;
            }

            BeginFracture();
            ReleaseChunksNear(
                worldPoint,
                impulseDirection,
                Mathf.Max(0f, impulseMagnitude),
                impactRadius);
        }

        /// <summary>Releases every generated chunk regardless of trigger mode.</summary>
        public void BreakAll()
        {
            if (!HasGeneratedFracture)
            {
                Debug.LogWarning(
                    $"{name} cannot break because no Apex fracture chunks have been generated.",
                    this);
                return;
            }

            BeginFracture();
            Vector3 point = generatedChunksRoot != null
                ? generatedChunksRoot.position
                : transform.position;
            ReleaseChunksNear(
                point,
                Vector3.up,
                ResolveManualBreakImpulse(),
                float.PositiveInfinity);
        }

        internal void HandleChunkCollision(
            ApexDestructibleChunk sourceChunk,
            Collision collision)
        {
            if (!fractured || collision == null)
            {
                return;
            }

            if (sourceChunk != null && sourceChunk.IsCollisionFromSameDestructible(collision))
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            if (impulse < secondaryBreakImpulse)
            {
                return;
            }

            Vector3 point = sourceChunk != null
                ? sourceChunk.transform.position
                : transform.position;
            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.0001f
                ? -collision.relativeVelocity.normalized
                : Vector3.up;

            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                point = contact.point;
                direction = -contact.normal;
            }

            ReleaseChunksNear(point, direction, impulse, impactRadius);
        }

        internal void NotifyChunkReleased(ApexDestructibleChunk chunk)
        {
            ChunkReleased?.Invoke(chunk);
        }

        private void HandleApexImpact(ApexImpactInfo impact)
        {
            if (fractured || fractureTrigger != ApexFractureTrigger.ImpactThreshold ||
                !ThresholdReached(impact.Impulse))
            {
                return;
            }

            Vector3 direction = impact.RelativeVelocity.sqrMagnitude > 0.0001f
                ? -impact.RelativeVelocity.normalized
                : -impact.Normal;
            BreakAt(impact.Point, direction, impact.Impulse);
        }

        private bool ThresholdReached(float collisionImpulse)
        {
            return MeasureThresholdValue(collisionImpulse) >= breakThreshold;
        }

        private float MeasureThresholdValue(float collisionImpulse)
        {
            float impulse = Mathf.Max(0f, collisionImpulse);
            if (thresholdMeasurement == ApexImpactThresholdMeasurement.EstimatedForce)
            {
                float step = Mathf.Max(0.0001f, Time.fixedDeltaTime);
                return impulse / step;
            }

            return impulse;
        }

        private float ResolveManualBreakImpulse()
        {
            if (thresholdMeasurement == ApexImpactThresholdMeasurement.EstimatedForce)
            {
                return breakThreshold * Mathf.Max(0.0001f, Time.fixedDeltaTime);
            }

            return breakThreshold;
        }

        private void BeginFracture()
        {
            if (fractured)
            {
                return;
            }

            fractured = true;
            CacheSourceMotion();
            DisableIntactObject();

            if (generatedChunksRoot != null)
            {
                generatedChunksRoot.gameObject.SetActive(true);
                generatedChunksRoot.SetParent(null, true);
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i]?.RevealLocked();
            }

            Fractured?.Invoke(this);
        }

        private void ReleaseChunksNear(
            Vector3 worldPoint,
            Vector3 impulseDirection,
            float impulseMagnitude,
            float radius)
        {
            if (chunks == null || chunks.Length == 0)
            {
                return;
            }

            bool releasedAny = false;
            float closestDistance = float.PositiveInfinity;
            ApexDestructibleChunk closestChunk = null;

            for (int i = 0; i < chunks.Length; i++)
            {
                ApexDestructibleChunk chunk = chunks[i];
                if (chunk == null || chunk.IsReleased)
                {
                    continue;
                }

                float distance = Vector3.Distance(worldPoint, chunk.WorldCenter);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestChunk = chunk;
                }

                if (!breakAllAtOnce && distance > radius)
                {
                    continue;
                }

                ReleaseChunk(chunk, worldPoint, impulseDirection, impulseMagnitude);
                releasedAny = true;
            }

            if (!releasedAny && closestChunk != null)
            {
                ReleaseChunk(closestChunk, worldPoint, impulseDirection, impulseMagnitude);
            }
        }

        private void ReleaseChunk(
            ApexDestructibleChunk chunk,
            Vector3 impactPoint,
            Vector3 impulseDirection,
            float impulseMagnitude)
        {
            Vector3 inheritedVelocity = preserveMomentum
                ? sourceLinearVelocity + Vector3.Cross(
                    sourceAngularVelocity,
                    chunk.WorldCenter - sourceCenterOfMass)
                : Vector3.zero;
            Vector3 inheritedAngularVelocity = preserveMomentum
                ? sourceAngularVelocity * inheritedAngularVelocityMultiplier
                : Vector3.zero;

            Vector3 outwardDirection = chunk.WorldCenter - impactPoint;
            if (outwardDirection.sqrMagnitude < 0.0001f)
            {
                outwardDirection = impulseDirection;
            }
            if (outwardDirection.sqrMagnitude < 0.0001f)
            {
                outwardDirection = Vector3.up;
            }

            Vector3 outwardImpulse =
                outwardDirection.normalized * impulseMagnitude * outwardImpulseMultiplier;

            chunk.Release(
                inheritedVelocity,
                inheritedAngularVelocity,
                outwardImpulse,
                impactPoint,
                destroyReleasedChunks ? debrisLifetime : 0f);
        }

        private void CacheSourceMotion()
        {
            CacheReferences();
            if (sourceBody == null)
            {
                sourceLinearVelocity = Vector3.zero;
                sourceAngularVelocity = Vector3.zero;
                sourceCenterOfMass = transform.position;
                return;
            }

            sourceLinearVelocity = sourceBody.velocity;
            sourceAngularVelocity = sourceBody.angularVelocity;
            sourceCenterOfMass = sourceBody.worldCenterOfMass;
        }

        private void DisableIntactObject()
        {
            for (int i = 0; i < intactRenderers.Length; i++)
            {
                if (intactRenderers[i] != null)
                {
                    intactRenderers[i].enabled = false;
                }
            }

            for (int i = 0; i < intactColliders.Length; i++)
            {
                if (intactColliders[i] != null)
                {
                    intactColliders[i].enabled = false;
                }
            }

            if (sourceBody != null)
            {
                sourceBody.detectCollisions = false;
                sourceBody.isKinematic = true;
                sourceBody.velocity = Vector3.zero;
                sourceBody.angularVelocity = Vector3.zero;
            }
        }

        private void PrepareGeneratedChunks()
        {
            if (generatedChunksRoot != null)
            {
                generatedChunksRoot.gameObject.SetActive(true);
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null)
                {
                    continue;
                }

                chunks[i].Configure(this);
                chunks[i].PrepareHidden();
            }
        }

        private void CacheReferences()
        {
            if (apexBody == null)
            {
                apexBody = GetComponent<ApexBody>();
            }

            if (sourceBody == null)
            {
                sourceBody = GetComponent<Rigidbody>();
            }
        }

#if UNITY_EDITOR
        public void EditorAssignGeneratedFracture(
            Transform chunkRoot,
            ApexDestructibleChunk[] generatedChunks,
            Renderer[] originalRenderers,
            Collider[] originalColliders,
            string assetFolder)
        {
            generatedChunksRoot = chunkRoot;
            chunks = generatedChunks ?? Array.Empty<ApexDestructibleChunk>();
            intactRenderers = originalRenderers ?? Array.Empty<Renderer>();
            intactColliders = originalColliders ?? Array.Empty<Collider>();
            generatedAssetFolder = assetFolder ?? string.Empty;
            apexBody = GetComponent<ApexBody>();
            sourceBody = GetComponent<Rigidbody>();
        }

        public void EditorClearGeneratedFracture()
        {
            generatedChunksRoot = null;
            chunks = Array.Empty<ApexDestructibleChunk>();
            intactRenderers = Array.Empty<Renderer>();
            intactColliders = Array.Empty<Collider>();
            generatedAssetFolder = string.Empty;
        }

        private void OnValidate()
        {
            breakThreshold = Mathf.Max(0f, breakThreshold);
            impactRadius = Mathf.Max(0.01f, impactRadius);
            secondaryBreakImpulse = Mathf.Max(0f, secondaryBreakImpulse);
            outwardImpulseMultiplier = Mathf.Max(0f, outwardImpulseMultiplier);
            inheritedAngularVelocityMultiplier = Mathf.Max(
                0f,
                inheritedAngularVelocityMultiplier);
            debrisLifetime = Mathf.Max(0.1f, debrisLifetime);
            CacheReferences();
        }
#endif
    }
}

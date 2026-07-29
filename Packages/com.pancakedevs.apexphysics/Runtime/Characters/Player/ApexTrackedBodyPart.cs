using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Force-drives a physical head, hand, or other body part toward a tracking target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexTrackedBodyPart : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private ApexPhysicalPlayerProfile profile;
        [SerializeField] private Transform target;
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private bool snapWhenTooFar = true;

        [Header("Collision")]
        [SerializeField] private bool ignoreOwnerCollisions = true;

        private Rigidbody cachedRigidbody;
        private Collider[] ownColliders;

        public ApexPhysicalPlayerProfile Profile => profile;
        public Transform Target => target;
        public Transform OwnerRoot => ownerRoot;
        public Rigidbody Body
        {
            get
            {
                CacheReferences();
                return cachedRigidbody;
            }
        }

        public Vector3 PositionError => target != null && Body != null
            ? target.position - Body.position
            : Vector3.zero;

        private void Reset()
        {
            CacheReferences();
            ConfigureBody();
        }

        private void Awake()
        {
            CacheReferences();
            ConfigureBody();
        }

        private void OnEnable()
        {
            CacheReferences();
            ConfigureBody();
            ReapplyIgnoredCollisions();
        }

        private void FixedUpdate()
        {
            if (profile == null || target == null || Body == null || Body.isKinematic)
            {
                return;
            }

            Vector3 error = target.position - Body.position;
            if (snapWhenTooFar && error.sqrMagnitude > profile.TrackedSnapDistance * profile.TrackedSnapDistance)
            {
                SnapToTarget();
                return;
            }

            Vector3 force = error * profile.TrackedPositionSpring -
                            Body.velocity * profile.TrackedPositionDamper;
            force = Vector3.ClampMagnitude(force, profile.TrackedMaximumForce);
            Body.AddForce(force, ForceMode.Force);

            Quaternion delta = target.rotation * Quaternion.Inverse(Body.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (axis.sqrMagnitude > 0.0001f && !float.IsNaN(axis.x))
            {
                Vector3 torque = axis.normalized *
                                 (angleDegrees * Mathf.Deg2Rad * profile.TrackedRotationSpring) -
                                 Body.angularVelocity * profile.TrackedRotationDamper;
                torque = Vector3.ClampMagnitude(torque, profile.TrackedMaximumTorque);
                Body.AddTorque(torque, ForceMode.Force);
            }
        }

        public void Configure(
            ApexPhysicalPlayerProfile newProfile,
            Transform newTarget,
            Transform newOwnerRoot)
        {
            profile = newProfile;
            target = newTarget;
            ownerRoot = newOwnerRoot;
            ConfigureBody();
            ReapplyIgnoredCollisions();
        }

        public void SetProfile(ApexPhysicalPlayerProfile newProfile)
        {
            profile = newProfile;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetOwnerRoot(Transform newOwnerRoot)
        {
            ownerRoot = newOwnerRoot;
            ReapplyIgnoredCollisions();
        }

        public void SnapToTarget()
        {
            if (target == null || Body == null)
            {
                return;
            }

            Body.position = target.position;
            Body.rotation = target.rotation;
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
        }

        public void ReapplyIgnoredCollisions()
        {
            if (!ignoreOwnerCollisions || ownerRoot == null)
            {
                return;
            }

            CacheReferences();
            Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(true);
            for (int ownIndex = 0; ownIndex < ownColliders.Length; ownIndex++)
            {
                Collider ownCollider = ownColliders[ownIndex];
                if (ownCollider == null)
                {
                    continue;
                }

                for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
                {
                    Collider ownerCollider = ownerColliders[ownerIndex];
                    if (ownerCollider == null || ownerCollider == ownCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, ownerCollider, true);
                }
            }
        }

        private void CacheReferences()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            if (ownColliders == null || ownColliders.Length == 0)
            {
                ownColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void ConfigureBody()
        {
            if (Body == null)
            {
                return;
            }

            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 40f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheReferences();
            ConfigureBody();
        }
#endif
    }
}

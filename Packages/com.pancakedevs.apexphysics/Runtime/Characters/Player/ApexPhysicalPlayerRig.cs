using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Input-agnostic, force-driven player root. External systems provide movement,
    /// turning, jump, crouch, and tracking targets through the public API.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(ApexBody))]
    public sealed class ApexPhysicalPlayerRig : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ApexPhysicalPlayerProfile profile;
        [SerializeField] private Transform orientationSource;
        [SerializeField] private bool useHeadHeightForCrouch = true;

        [Header("Tracking Targets")]
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;

        [Header("Physical Tracked Parts")]
        [SerializeField] private ApexTrackedBodyPart physicalHead;
        [SerializeField] private ApexTrackedBodyPart physicalLeftHand;
        [SerializeField] private ApexTrackedBodyPart physicalRightHand;

        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private readonly Collider[] overlapHits = new Collider[16];

        private Rigidbody cachedRigidbody;
        private CapsuleCollider cachedCapsule;
        private ApexBody cachedApexBody;
        private Vector2 movementInput;
        private float turnInput;
        private bool jumpRequested;
        private bool crouchRequested;
        private bool grounded;
        private Vector3 groundNormal = Vector3.up;
        private float targetYaw;

        public ApexPhysicalPlayerProfile Profile => profile;
        public Rigidbody Rigidbody
        {
            get
            {
                CacheReferences();
                return cachedRigidbody;
            }
        }

        public CapsuleCollider BodyCollider
        {
            get
            {
                CacheReferences();
                return cachedCapsule;
            }
        }

        public ApexBody ApexBody
        {
            get
            {
                CacheReferences();
                return cachedApexBody;
            }
        }

        public Transform HeadTarget => headTarget;
        public Transform LeftHandTarget => leftHandTarget;
        public Transform RightHandTarget => rightHandTarget;
        public bool IsGrounded => grounded;
        public Vector3 GroundNormal => groundNormal;
        public Vector2 MovementInput => movementInput;
        public float TurnInput => turnInput;
        public bool IsCrouching => crouchRequested;
        public Vector3 HorizontalVelocity => Rigidbody != null
            ? Vector3.ProjectOnPlane(Rigidbody.velocity, Vector3.up)
            : Vector3.zero;

        private void Reset()
        {
            CacheReferences();
            ApplyProfile();
        }

        private void Awake()
        {
            CacheReferences();
            targetYaw = transform.eulerAngles.y;
            ApplyProfile();
        }

        private void OnEnable()
        {
            ConfigureTrackedParts();
        }

        private void FixedUpdate()
        {
            if (profile == null || Rigidbody == null || Rigidbody.isKinematic)
            {
                jumpRequested = false;
                return;
            }

            UpdateCapsuleHeight();
            UpdateGrounding();
            ApplyMovement();
            ApplyTurning();
            ApplyJump();
            jumpRequested = false;
        }

        public void SetProfile(ApexPhysicalPlayerProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;
            if (applyImmediately)
            {
                ApplyProfile();
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            movementInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetTurnInput(float input)
        {
            turnInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public void SetCrouch(bool crouching)
        {
            crouchRequested = crouching;
        }

        public void SnapTurn(float degrees)
        {
            targetYaw += degrees;
        }

        public void ClearInput()
        {
            movementInput = Vector2.zero;
            turnInput = 0f;
            jumpRequested = false;
        }

        public void SetTrackingTargets(
            Transform newHeadTarget,
            Transform newLeftHandTarget,
            Transform newRightHandTarget)
        {
            headTarget = newHeadTarget;
            leftHandTarget = newLeftHandTarget;
            rightHandTarget = newRightHandTarget;

            if (orientationSource == null)
            {
                orientationSource = headTarget;
            }

            ConfigureTrackedParts();
        }

        public void SetTrackedBodies(
            ApexTrackedBodyPart newPhysicalHead,
            ApexTrackedBodyPart newPhysicalLeftHand,
            ApexTrackedBodyPart newPhysicalRightHand)
        {
            physicalHead = newPhysicalHead;
            physicalLeftHand = newPhysicalLeftHand;
            physicalRightHand = newPhysicalRightHand;
            ConfigureTrackedParts();
        }

        public void ApplyProfile()
        {
            CacheReferences();
            if (profile == null || Rigidbody == null || BodyCollider == null)
            {
                return;
            }

            Rigidbody.mass = profile.BodyMass;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rigidbody.constraints |= RigidbodyConstraints.FreezeRotationX |
                                     RigidbodyConstraints.FreezeRotationZ;

            BodyCollider.direction = 1;
            BodyCollider.radius = profile.BodyRadius;
            BodyCollider.height = Mathf.Max(profile.StandingHeight, profile.BodyRadius * 2f);
            BodyCollider.center = Vector3.up * (BodyCollider.height * 0.5f);

            ConfigureTrackedParts();
        }

        public void Teleport(Vector3 position, Quaternion rotation, bool preserveVelocity = false)
        {
            if (ApexBody != null)
            {
                ApexBody.Teleport(position, rotation, preserveVelocity);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            targetYaw = rotation.eulerAngles.y;
            physicalHead?.SnapToTarget();
            physicalLeftHand?.SnapToTarget();
            physicalRightHand?.SnapToTarget();
        }

        private void ApplyMovement()
        {
            Vector3 forward = orientationSource != null ? orientationSource.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 desiredDirection = forward * movementInput.y + right * movementInput.x;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

            Vector3 desiredVelocity = desiredDirection * profile.MovementSpeed;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(Rigidbody.velocity, Vector3.up);
            float response = desiredDirection.sqrMagnitude > 0.0001f
                ? (grounded ? profile.GroundAcceleration : profile.AirAcceleration)
                : profile.Braking;

            Vector3 acceleration = (desiredVelocity - currentVelocity) * response;
            acceleration = Vector3.ClampMagnitude(
                acceleration,
                profile.MaximumMovementAcceleration);
            Rigidbody.AddForce(acceleration, ForceMode.Acceleration);
        }

        private void ApplyTurning()
        {
            targetYaw += turnInput * profile.TurnSpeed * Time.fixedDeltaTime;
            float currentYaw = Rigidbody.rotation.eulerAngles.y;
            float angleError = Mathf.DeltaAngle(currentYaw, targetYaw);
            float torque = angleError * Mathf.Deg2Rad * profile.TurnSpring -
                           Rigidbody.angularVelocity.y * profile.TurnDamping;
            torque = Mathf.Clamp(torque, -profile.MaximumTurnTorque, profile.MaximumTurnTorque);
            Rigidbody.AddTorque(Vector3.up * torque, ForceMode.Acceleration);
        }

        private void ApplyJump()
        {
            if (!jumpRequested || !grounded)
            {
                return;
            }

            Rigidbody.AddForce(Vector3.up * profile.JumpVelocity, ForceMode.VelocityChange);
            grounded = false;
        }

        private void UpdateGrounding()
        {
            float radius = Mathf.Min(
                profile.GroundProbeRadius,
                Mathf.Max(0.01f, BodyCollider.radius * 0.95f));
            Vector3 worldCenter = transform.TransformPoint(BodyCollider.center);
            float halfHeight = Mathf.Max(BodyCollider.height * 0.5f, BodyCollider.radius);
            Vector3 origin = worldCenter - Vector3.up * (halfHeight - radius - 0.05f);
            float distance = profile.GroundProbeDistance + 0.05f;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                groundHits,
                distance,
                profile.GroundLayers,
                QueryTriggerInteraction.Ignore);

            grounded = false;
            groundNormal = Vector3.up;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    grounded = true;
                    groundNormal = hit.normal;
                }
            }
        }

        private void UpdateCapsuleHeight()
        {
            float desiredHeight = crouchRequested
                ? profile.CrouchingHeight
                : profile.StandingHeight;

            if (useHeadHeightForCrouch && headTarget != null)
            {
                float trackedHeight = transform.InverseTransformPoint(headTarget.position).y +
                                      profile.BodyRadius;
                desiredHeight = Mathf.Clamp(
                    trackedHeight,
                    profile.CrouchingHeight,
                    profile.StandingHeight);
            }

            desiredHeight = Mathf.Max(desiredHeight, profile.BodyRadius * 2f);
            float nextHeight = Mathf.MoveTowards(
                BodyCollider.height,
                desiredHeight,
                profile.CrouchSpeed * Time.fixedDeltaTime);

            if (nextHeight > BodyCollider.height && !CanUseCapsuleHeight(nextHeight))
            {
                return;
            }

            BodyCollider.height = nextHeight;
            BodyCollider.radius = Mathf.Min(profile.BodyRadius, nextHeight * 0.5f);
            BodyCollider.center = Vector3.up * (nextHeight * 0.5f);
        }

        private bool CanUseCapsuleHeight(float height)
        {
            float radius = Mathf.Min(profile.BodyRadius, height * 0.5f) * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * Mathf.Max(radius, height - radius);

            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                overlapHits,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider overlap = overlapHits[i];
                if (overlap != null && !overlap.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        private void ConfigureTrackedParts()
        {
            if (profile == null)
            {
                return;
            }

            physicalHead?.Configure(profile, headTarget, transform);
            physicalLeftHand?.Configure(profile, leftHandTarget, transform);
            physicalRightHand?.Configure(profile, rightHandTarget, transform);
        }

        private void CacheReferences()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            if (cachedCapsule == null)
            {
                cachedCapsule = GetComponent<CapsuleCollider>();
            }

            if (cachedApexBody == null)
            {
                cachedApexBody = GetComponent<ApexBody>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheReferences();
            if (!Application.isPlaying)
            {
                ApplyProfile();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (profile == null || BodyCollider == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(
                transform.position + Vector3.up * profile.GroundProbeRadius,
                profile.GroundProbeRadius);
        }
#endif
    }
}

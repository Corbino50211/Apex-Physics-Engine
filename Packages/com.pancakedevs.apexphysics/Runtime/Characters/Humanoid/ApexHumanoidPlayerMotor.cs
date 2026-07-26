using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Input-agnostic locomotion for a fully articulated physical humanoid.
    /// It drives the physical hips without adding a second capsule-based player body.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexHumanoidPlayerMotor : MonoBehaviour
    {
        [SerializeField] private ApexPhysicalPlayerProfile profile;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private Transform orientationSource;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [Header("Balance")]
        [SerializeField, Min(0f)] private float balanceSpring = 140f;
        [SerializeField, Min(0f)] private float balanceDamper = 18f;
        [SerializeField, Min(0f)] private float maximumBalanceTorque = 180f;

        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private Rigidbody cachedRigidbody;
        private Vector2 moveInput;
        private float turnInput;
        private float targetYaw;
        private bool jumpRequested;
        private bool grounded;
        private Vector3 groundNormal = Vector3.up;

        public ApexPhysicalPlayerProfile Profile => profile;
        public ApexActiveRagdoll ActiveRagdoll => activeRagdoll;
        public Transform LeftFoot => leftFoot;
        public Transform RightFoot => rightFoot;
        public bool IsGrounded => grounded;
        public Vector3 GroundNormal => groundNormal;
        public Vector2 MoveInput => moveInput;
        public float TurnInput => turnInput;
        public Rigidbody Rigidbody => cachedRigidbody != null
            ? cachedRigidbody
            : cachedRigidbody = GetComponent<Rigidbody>();

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            targetYaw = transform.eulerAngles.y;
            RefreshFootReferences();
        }

        private void FixedUpdate()
        {
            if (profile == null || Rigidbody == null || Rigidbody.isKinematic)
            {
                jumpRequested = false;
                return;
            }

            UpdateGrounding();

            float controlStrength = GetControlStrength();
            if (controlStrength > 0f)
            {
                ApplyMovement(controlStrength);
                ApplyTurningAndBalance(controlStrength);
                ApplyJump(controlStrength);
            }

            jumpRequested = false;
        }

        public void Configure(
            ApexPhysicalPlayerProfile newProfile,
            ApexActiveRagdoll ragdoll,
            Transform newOrientationSource)
        {
            profile = newProfile;
            activeRagdoll = ragdoll;
            orientationSource = newOrientationSource;
            targetYaw = transform.eulerAngles.y;
            RefreshFootReferences();
        }

        public void SetFootReferences(Transform newLeftFoot, Transform newRightFoot)
        {
            leftFoot = newLeftFoot;
            rightFoot = newRightFoot;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetTurnInput(float input)
        {
            turnInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public void SnapTurn(float degrees)
        {
            targetYaw += degrees;
        }

        public void ClearInput()
        {
            moveInput = Vector2.zero;
            turnInput = 0f;
            jumpRequested = false;
        }

        public void RefreshFootReferences()
        {
            Animator physicalAnimator = GetComponentInParent<Animator>();
            if (physicalAnimator == null || !physicalAnimator.isHuman)
            {
                return;
            }

            leftFoot = physicalAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = physicalAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private float GetControlStrength()
        {
            if (activeRagdoll == null)
            {
                return 1f;
            }

            switch (activeRagdoll.State)
            {
                case ApexRagdollState.Active:
                    return 1f;
                case ApexRagdollState.Recovering:
                    return activeRagdoll.RecoveryProgress;
                default:
                    return 0f;
            }
        }

        private void ApplyMovement(float strength)
        {
            Vector3 forward = orientationSource != null ? orientationSource.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 desiredDirection = Vector3.ClampMagnitude(
                forward * moveInput.y + right * moveInput.x,
                1f);
            Vector3 desiredVelocity = desiredDirection * profile.MovementSpeed;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(Rigidbody.velocity, Vector3.up);
            float response = desiredDirection.sqrMagnitude > 0.0001f
                ? (grounded ? profile.GroundAcceleration : profile.AirAcceleration)
                : profile.Braking;

            Vector3 acceleration = (desiredVelocity - currentVelocity) * response;
            acceleration = Vector3.ClampMagnitude(
                acceleration,
                profile.MaximumMovementAcceleration) * strength;
            Rigidbody.AddForce(acceleration, ForceMode.Acceleration);
        }

        private void ApplyTurningAndBalance(float strength)
        {
            targetYaw += turnInput * profile.TurnSpeed * Time.fixedDeltaTime;
            Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
            Quaternion delta = targetRotation * Quaternion.Inverse(Rigidbody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (axis.sqrMagnitude < 0.0001f || float.IsNaN(axis.x))
            {
                return;
            }

            Vector3 torque = axis.normalized *
                             (angleDegrees * Mathf.Deg2Rad * balanceSpring) -
                             Rigidbody.angularVelocity * balanceDamper;
            torque = Vector3.ClampMagnitude(torque, maximumBalanceTorque) * strength;
            Rigidbody.AddTorque(torque, ForceMode.Acceleration);
        }

        private void ApplyJump(float strength)
        {
            if (!jumpRequested || !grounded)
            {
                return;
            }

            Rigidbody.AddForce(
                Vector3.up * profile.JumpVelocity * strength,
                ForceMode.VelocityChange);
            grounded = false;
        }

        private void UpdateGrounding()
        {
            if (leftFoot == null && rightFoot == null)
            {
                RefreshFootReferences();
            }

            float radius = Mathf.Max(0.03f, profile.GroundProbeRadius * 0.5f);
            Vector3 footPosition;
            if (leftFoot != null && rightFoot != null)
            {
                footPosition = (leftFoot.position + rightFoot.position) * 0.5f;
            }
            else if (leftFoot != null)
            {
                footPosition = leftFoot.position;
            }
            else if (rightFoot != null)
            {
                footPosition = rightFoot.position;
            }
            else
            {
                footPosition = Rigidbody.worldCenterOfMass - Vector3.up * 0.8f;
            }

            Vector3 origin = footPosition + Vector3.up * (radius + 0.03f);
            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                groundHits,
                profile.GroundProbeDistance + 0.06f,
                profile.GroundLayers,
                QueryTriggerInteraction.Ignore);

            grounded = false;
            groundNormal = Vector3.up;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null ||
                    hit.rigidbody == Rigidbody ||
                    hit.collider.transform.IsChildOf(transform.root))
                {
                    continue;
                }

                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    grounded = true;
                    groundNormal = hit.normal;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            balanceSpring = Mathf.Max(0f, balanceSpring);
            balanceDamper = Mathf.Max(0f, balanceDamper);
            maximumBalanceTorque = Mathf.Max(0f, maximumBalanceTorque);
        }
#endif
    }
}

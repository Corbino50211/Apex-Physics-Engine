using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Input-agnostic Rigidbody motor for the desktop Apex player rig.
    /// Movement input is supplied by an input adapter, AI, networking, or tests.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class ApexPCPlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform movementReference;
        [SerializeField] private Transform groundProbe;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float groundAcceleration = 35f;
        [SerializeField, Min(0f)] private float airAcceleration = 10f;
        [SerializeField, Min(0f)] private float jumpVelocity = 6f;

        [Header("Grounding")]
        [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.28f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.18f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Range(0f, 89f)] private float maximumGroundAngle = 55f;
        [SerializeField, Min(0f)] private float groundedStickForce = 4f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private Vector2 moveInput;
        private bool sprintHeld;
        private bool jumpQueued;
        private bool grounded;
        private Vector3 groundNormal = Vector3.up;

        public Rigidbody Rigidbody => body;
        public bool IsGrounded => grounded;
        public Vector3 GroundNormal => groundNormal;
        public Vector2 MoveInput => moveInput;

        private void Reset()
        {
            CacheComponents();
            ConfigureBody();
            movementReference = transform;
        }

        private void Awake()
        {
            CacheComponents();
            ConfigureBody();
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();
            ApplyMovement();
            ApplyJump();
            jumpQueued = false;
        }

        public void SetMoveInput(Vector2 value)
        {
            moveInput = Vector2.ClampMagnitude(value, 1f);
        }

        public void SetSprint(bool value)
        {
            sprintHeld = value;
        }

        public void QueueJump()
        {
            jumpQueued = true;
        }

        public void ClearInput()
        {
            moveInput = Vector2.zero;
            sprintHeld = false;
            jumpQueued = false;
        }

        private void ApplyMovement()
        {
            Transform reference = movementReference != null ? movementReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            Vector3 desiredDirection = Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);

            if (grounded)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, groundNormal).normalized * desiredDirection.magnitude;
            }

            float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = desiredDirection * targetSpeed;
            Vector3 currentPlanar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float acceleration = grounded ? groundAcceleration : airAcceleration;
            Vector3 velocityChange = Vector3.ClampMagnitude(
                targetVelocity - currentPlanar,
                acceleration * Time.fixedDeltaTime);

            body.AddForce(velocityChange, ForceMode.VelocityChange);

            if (grounded && body.linearVelocity.y <= 0f && groundedStickForce > 0f)
            {
                body.AddForce(-groundNormal * groundedStickForce, ForceMode.Acceleration);
            }
        }

        private void ApplyJump()
        {
            if (!jumpQueued || !grounded)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }

            body.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
            grounded = false;
        }

        private void UpdateGroundedState()
        {
            Vector3 origin = groundProbe != null
                ? groundProbe.position
                : transform.TransformPoint(capsule.center) + Vector3.down * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);

            grounded = Physics.SphereCast(
                origin + Vector3.up * 0.03f,
                groundProbeRadius,
                Vector3.down,
                out RaycastHit hit,
                groundProbeDistance + 0.03f,
                groundLayers,
                triggerInteraction) &&
                Vector3.Angle(hit.normal, Vector3.up) <= maximumGroundAngle;

            groundNormal = grounded ? hit.normal : Vector3.up;
        }

        private void CacheComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (capsule == null)
            {
                capsule = GetComponent<CapsuleCollider>();
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            groundAcceleration = Mathf.Max(0f, groundAcceleration);
            airAcceleration = Mathf.Max(0f, airAcceleration);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
            groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        }

        private void OnDrawGizmosSelected()
        {
            CapsuleCollider currentCapsule = capsule != null ? capsule : GetComponent<CapsuleCollider>();
            if (currentCapsule == null)
            {
                return;
            }

            Vector3 origin = groundProbe != null
                ? groundProbe.position
                : transform.TransformPoint(currentCapsule.center) + Vector3.down * Mathf.Max(0f, currentCapsule.height * 0.5f - currentCapsule.radius);
            Gizmos.DrawWireSphere(origin + Vector3.down * groundProbeDistance, groundProbeRadius);
        }
#endif
    }
}

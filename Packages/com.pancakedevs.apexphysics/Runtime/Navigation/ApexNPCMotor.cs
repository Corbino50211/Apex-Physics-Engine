using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Force-driven movement layer for a basic physical Apex NPC.
    /// The navigator plans the path; this motor pushes and turns the Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexBody))]
    [RequireComponent(typeof(ApexNPCNavigator))]
    public sealed class ApexNPCMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 3.5f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float braking = 24f;
        [SerializeField, Min(0f)] private float maximumMoveForce = 250f;

        [Header("Turning")]
        [SerializeField] private bool faceMovementDirection = true;
        [SerializeField, Min(0f)] private float turnResponsiveness = 8f;
        [SerializeField, Min(0f)] private float turnAcceleration = 18f;
        [SerializeField, Min(0f)] private float maximumTurnSpeed = 360f;
        [SerializeField, Min(0f)] private float maximumTurnTorque = 250f;

        [Header("Upright Stabilization")]
        [SerializeField] private bool stabilizeUpright = true;
        [SerializeField, Min(0f)] private float uprightSpring = 80f;
        [SerializeField, Min(0f)] private float uprightDamping = 10f;
        [SerializeField, Min(0f)] private float maximumUprightTorque = 300f;

        [Header("Rigidbody Setup")]
        [SerializeField] private bool configureInterpolation = true;
        [SerializeField] private bool configureContinuousCollision = true;

        private ApexBody cachedBody;
        private ApexNPCNavigator cachedNavigator;
        private Vector3 requestedVelocity;

        public ApexBody Body
        {
            get
            {
                CacheReferences();
                return cachedBody;
            }
        }

        public ApexNPCNavigator Navigator
        {
            get
            {
                CacheReferences();
                return cachedNavigator;
            }
        }

        public Vector3 RequestedVelocity => requestedVelocity;
        public bool IsMoving => requestedVelocity.sqrMagnitude > 0.0001f;

        private void Reset()
        {
            CacheReferences();
            ConfigureRigidbody();
        }

        private void Awake()
        {
            CacheReferences();
            ConfigureRigidbody();
        }

        private void FixedUpdate()
        {
            CacheReferences();

            Rigidbody rigidbody = Body != null ? Body.Rigidbody : null;
            if (rigidbody == null || rigidbody.isKinematic || Navigator == null)
            {
                requestedVelocity = Vector3.zero;
                return;
            }

            requestedVelocity = CalculateRequestedVelocity(rigidbody.position);
            ApplyMovement(rigidbody, requestedVelocity);

            if (faceMovementDirection)
            {
                ApplyTurning(rigidbody, requestedVelocity);
            }

            if (stabilizeUpright)
            {
                ApplyUprightStabilization(rigidbody);
            }
        }

        private Vector3 CalculateRequestedVelocity(Vector3 bodyPosition)
        {
            if (!Navigator.HasDestination ||
                Navigator.HasReachedDestination ||
                Navigator.IsPathPending ||
                !Navigator.HasPath)
            {
                return Vector3.zero;
            }

            Vector3 direction = Navigator.SteeringTarget - bodyPosition;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.ProjectOnPlane(Navigator.DesiredVelocity, Vector3.up);
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            float slowdownDistance = Mathf.Max(
                Navigator.Agent != null ? Navigator.Agent.stoppingDistance * 2f : 1f,
                0.1f);
            float speedMultiplier = Mathf.Clamp01(Navigator.RemainingDistance / slowdownDistance);

            return direction.normalized * (movementSpeed * speedMultiplier);
        }

        private void ApplyMovement(Rigidbody rigidbody, Vector3 desiredVelocity)
        {
            Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(rigidbody.velocity, Vector3.up);
            Vector3 velocityError = desiredVelocity - currentHorizontalVelocity;
            float accelerationLimit = desiredVelocity.sqrMagnitude > 0.0001f
                ? acceleration
                : braking;

            float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            Vector3 requiredAcceleration = velocityError / deltaTime;
            requiredAcceleration = Vector3.ClampMagnitude(requiredAcceleration, accelerationLimit);

            Vector3 force = requiredAcceleration * rigidbody.mass;
            if (maximumMoveForce > 0f)
            {
                force = Vector3.ClampMagnitude(force, maximumMoveForce);
            }

            Body.ApplyForce(force, ForceMode.Force);
        }

        private void ApplyTurning(Rigidbody rigidbody, Vector3 desiredVelocity)
        {
            Vector3 desiredDirection = Vector3.ProjectOnPlane(desiredVelocity, Vector3.up);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (currentForward.sqrMagnitude <= 0.0001f)
            {
                currentForward = Vector3.forward;
            }

            float angleError = Vector3.SignedAngle(
                currentForward.normalized,
                desiredDirection.normalized,
                Vector3.up);

            float maximumTurnSpeedRadians = maximumTurnSpeed * Mathf.Deg2Rad;
            float desiredYawVelocity = Mathf.Clamp(
                angleError * Mathf.Deg2Rad * turnResponsiveness,
                -maximumTurnSpeedRadians,
                maximumTurnSpeedRadians);

            float currentYawVelocity = Vector3.Dot(rigidbody.angularVelocity, Vector3.up);
            float yawVelocityError = desiredYawVelocity - currentYawVelocity;
            float torqueMagnitude = yawVelocityError * turnAcceleration * rigidbody.mass;

            if (maximumTurnTorque > 0f)
            {
                torqueMagnitude = Mathf.Clamp(
                    torqueMagnitude,
                    -maximumTurnTorque,
                    maximumTurnTorque);
            }

            Body.ApplyTorque(Vector3.up * torqueMagnitude, ForceMode.Force);
        }

        private void ApplyUprightStabilization(Rigidbody rigidbody)
        {
            Vector3 tiltAxis = Vector3.Cross(transform.up, Vector3.up);
            Vector3 tiltAngularVelocity = Vector3.ProjectOnPlane(
                rigidbody.angularVelocity,
                Vector3.up);

            Vector3 torque = tiltAxis * uprightSpring * rigidbody.mass -
                             tiltAngularVelocity * uprightDamping * rigidbody.mass;

            if (maximumUprightTorque > 0f)
            {
                torque = Vector3.ClampMagnitude(torque, maximumUprightTorque);
            }

            Body.ApplyTorque(torque, ForceMode.Force);
        }

        private void ConfigureRigidbody()
        {
            Rigidbody rigidbody = Body != null ? Body.Rigidbody : null;
            if (rigidbody == null)
            {
                return;
            }

            if (configureInterpolation && rigidbody.interpolation == RigidbodyInterpolation.None)
            {
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (configureContinuousCollision &&
                rigidbody.collisionDetectionMode == CollisionDetectionMode.Discrete)
            {
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private void CacheReferences()
        {
            if (cachedBody == null)
            {
                cachedBody = GetComponent<ApexBody>();
            }

            if (cachedNavigator == null)
            {
                cachedNavigator = GetComponent<ApexNPCNavigator>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            maximumMoveForce = Mathf.Max(0f, maximumMoveForce);
            turnResponsiveness = Mathf.Max(0f, turnResponsiveness);
            turnAcceleration = Mathf.Max(0f, turnAcceleration);
            maximumTurnSpeed = Mathf.Max(0f, maximumTurnSpeed);
            maximumTurnTorque = Mathf.Max(0f, maximumTurnTorque);
            uprightSpring = Mathf.Max(0f, uprightSpring);
            uprightDamping = Mathf.Max(0f, uprightDamping);
            maximumUprightTorque = Mathf.Max(0f, maximumUprightTorque);
            CacheReferences();
        }
#endif
    }
}

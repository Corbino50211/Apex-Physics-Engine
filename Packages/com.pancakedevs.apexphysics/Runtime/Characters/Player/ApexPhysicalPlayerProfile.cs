using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    [CreateAssetMenu(
        fileName = "Apex Physical Player Profile",
        menuName = "PancakeDevs/Apex Physics/Physical Player Profile")]
    public sealed class ApexPhysicalPlayerProfile : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 4.5f;
        [SerializeField, Min(0f)] private float groundAcceleration = 28f;
        [SerializeField, Min(0f)] private float airAcceleration = 8f;
        [SerializeField, Min(0f)] private float braking = 20f;
        [SerializeField, Min(0f)] private float maximumMovementAcceleration = 40f;

        [Header("Turning")]
        [SerializeField, Min(0f)] private float turnSpeed = 180f;
        [SerializeField, Min(0f)] private float turnSpring = 24f;
        [SerializeField, Min(0f)] private float turnDamping = 5f;
        [SerializeField, Min(0f)] private float maximumTurnTorque = 35f;

        [Header("Jump and Grounding")]
        [SerializeField, Min(0f)] private float jumpVelocity = 5f;
        [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.25f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.2f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Body")]
        [SerializeField, Min(0.1f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.1f)] private float crouchingHeight = 1f;
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.3f;
        [SerializeField, Min(0f)] private float crouchSpeed = 6f;
        [SerializeField, Min(0.01f)] private float bodyMass = 75f;

        [Header("Tracked Body Parts")]
        [SerializeField, Min(0f)] private float trackedPositionSpring = 700f;
        [SerializeField, Min(0f)] private float trackedPositionDamper = 70f;
        [SerializeField, Min(0f)] private float trackedMaximumForce = 2500f;
        [SerializeField, Min(0f)] private float trackedRotationSpring = 550f;
        [SerializeField, Min(0f)] private float trackedRotationDamper = 55f;
        [SerializeField, Min(0f)] private float trackedMaximumTorque = 1800f;
        [SerializeField, Min(0.01f)] private float trackedSnapDistance = 2f;

        public float MovementSpeed => movementSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float AirAcceleration => airAcceleration;
        public float Braking => braking;
        public float MaximumMovementAcceleration => maximumMovementAcceleration;
        public float TurnSpeed => turnSpeed;
        public float TurnSpring => turnSpring;
        public float TurnDamping => turnDamping;
        public float MaximumTurnTorque => maximumTurnTorque;
        public float JumpVelocity => jumpVelocity;
        public float GroundProbeRadius => groundProbeRadius;
        public float GroundProbeDistance => groundProbeDistance;
        public LayerMask GroundLayers => groundLayers;
        public float StandingHeight => standingHeight;
        public float CrouchingHeight => crouchingHeight;
        public float BodyRadius => bodyRadius;
        public float CrouchSpeed => crouchSpeed;
        public float BodyMass => bodyMass;
        public float TrackedPositionSpring => trackedPositionSpring;
        public float TrackedPositionDamper => trackedPositionDamper;
        public float TrackedMaximumForce => trackedMaximumForce;
        public float TrackedRotationSpring => trackedRotationSpring;
        public float TrackedRotationDamper => trackedRotationDamper;
        public float TrackedMaximumTorque => trackedMaximumTorque;
        public float TrackedSnapDistance => trackedSnapDistance;

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            groundAcceleration = Mathf.Max(0f, groundAcceleration);
            airAcceleration = Mathf.Max(0f, airAcceleration);
            braking = Mathf.Max(0f, braking);
            maximumMovementAcceleration = Mathf.Max(0f, maximumMovementAcceleration);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            turnSpring = Mathf.Max(0f, turnSpring);
            turnDamping = Mathf.Max(0f, turnDamping);
            maximumTurnTorque = Mathf.Max(0f, maximumTurnTorque);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
            groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
            standingHeight = Mathf.Max(0.1f, standingHeight);
            crouchingHeight = Mathf.Clamp(crouchingHeight, 0.1f, standingHeight);
            bodyRadius = Mathf.Clamp(bodyRadius, 0.05f, standingHeight * 0.5f);
            crouchSpeed = Mathf.Max(0f, crouchSpeed);
            bodyMass = Mathf.Max(0.01f, bodyMass);
            trackedPositionSpring = Mathf.Max(0f, trackedPositionSpring);
            trackedPositionDamper = Mathf.Max(0f, trackedPositionDamper);
            trackedMaximumForce = Mathf.Max(0f, trackedMaximumForce);
            trackedRotationSpring = Mathf.Max(0f, trackedRotationSpring);
            trackedRotationDamper = Mathf.Max(0f, trackedRotationDamper);
            trackedMaximumTorque = Mathf.Max(0f, trackedMaximumTorque);
            trackedSnapDistance = Mathf.Max(0.01f, trackedSnapDistance);
        }
#endif
    }
}

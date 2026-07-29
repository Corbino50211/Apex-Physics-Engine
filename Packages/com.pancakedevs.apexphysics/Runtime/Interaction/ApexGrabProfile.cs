using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Reusable spring, damping, break, and release settings for an ApexGrabber.
    /// Lower forces make heavy objects lag naturally instead of snapping to a hand.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Apex Grab Profile",
        menuName = "PancakeDevs/Apex Physics/Grab Profile",
        order = 20)]
    public sealed class ApexGrabProfile : ScriptableObject
    {
        [Header("Position Drive")]
        [SerializeField, Min(0f)] private float positionSpring = 1200f;
        [SerializeField, Min(0f)] private float positionDamping = 90f;
        [SerializeField, Min(0f)] private float maximumForce = 1800f;

        [Header("Rotation Drive")]
        [SerializeField, Min(0f)] private float rotationSpring = 700f;
        [SerializeField, Min(0f)] private float rotationDamping = 45f;
        [SerializeField, Min(0f)] private float maximumTorque = 900f;

        [Header("Grip Limits")]
        [SerializeField, Min(0.01f)] private float breakDistance = 0.75f;
        [SerializeField, Min(0f)] private float breakForce = 3000f;
        [SerializeField, Min(0f)] private float maximumHeldMass = 150f;

        [Header("Release")]
        [SerializeField, Range(0f, 1f)] private float releaseVelocityInfluence = 0.75f;
        [SerializeField, Min(0f)] private float throwVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float maximumThrowSpeed = 25f;

        public float PositionSpring => positionSpring;
        public float PositionDamping => positionDamping;
        public float MaximumForce => maximumForce;
        public float RotationSpring => rotationSpring;
        public float RotationDamping => rotationDamping;
        public float MaximumTorque => maximumTorque;
        public float BreakDistance => breakDistance;
        public float BreakForce => breakForce;
        public float MaximumHeldMass => maximumHeldMass;
        public float ReleaseVelocityInfluence => releaseVelocityInfluence;
        public float ThrowVelocityMultiplier => throwVelocityMultiplier;
        public float MaximumThrowSpeed => maximumThrowSpeed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            positionSpring = Mathf.Max(0f, positionSpring);
            positionDamping = Mathf.Max(0f, positionDamping);
            maximumForce = Mathf.Max(0f, maximumForce);
            rotationSpring = Mathf.Max(0f, rotationSpring);
            rotationDamping = Mathf.Max(0f, rotationDamping);
            maximumTorque = Mathf.Max(0f, maximumTorque);
            breakDistance = Mathf.Max(0.01f, breakDistance);
            breakForce = Mathf.Max(0f, breakForce);
            maximumHeldMass = Mathf.Max(0f, maximumHeldMass);
            throwVelocityMultiplier = Mathf.Max(0f, throwVelocityMultiplier);
            maximumThrowSpeed = Mathf.Max(0f, maximumThrowSpeed);
        }
#endif
    }
}

using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Reusable Rigidbody settings for Apex objects. Profiles let a developer
    /// tune groups of objects consistently without editing every prefab.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Apex Physics Profile",
        menuName = "PancakeDevs/Apex Physics/Physics Profile",
        order = 1)]
    public sealed class ApexPhysicsProfile : ScriptableObject
    {
        [Header("Body")]
        [SerializeField, Min(0.0001f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float drag;
        [SerializeField, Min(0f)] private float angularDrag = 0.05f;
        [SerializeField] private bool useGravity = true;
        [SerializeField] private bool isKinematic;

        [Header("Simulation")]
        [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;
        [SerializeField, Min(0f)] private float sleepThreshold = 0.005f;

        [Header("Safety Limits")]
        [SerializeField, Min(0.01f)] private float maximumLinearSpeed = 75f;
        [SerializeField, Min(0.01f)] private float maximumAngularSpeed = 50f;

        public float Mass => mass;
        public float Drag => drag;
        public float AngularDrag => angularDrag;
        public bool UseGravity => useGravity;
        public bool IsKinematic => isKinematic;
        public RigidbodyInterpolation Interpolation => interpolation;
        public CollisionDetectionMode CollisionDetection => collisionDetection;
        public float SleepThreshold => sleepThreshold;
        public float MaximumLinearSpeed => maximumLinearSpeed;
        public float MaximumAngularSpeed => maximumAngularSpeed;

        /// <summary>Applies this profile to a Rigidbody.</summary>
        public void ApplyTo(Rigidbody body)
        {
            if (body == null)
            {
                return;
            }

            body.mass = mass;
            body.drag = drag;
            body.angularDrag = angularDrag;
            body.useGravity = useGravity;
            body.isKinematic = isKinematic;
            body.interpolation = interpolation;
            body.collisionDetectionMode = isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : collisionDetection;
            body.sleepThreshold = sleepThreshold;
            body.maxAngularVelocity = maximumAngularSpeed;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            mass = Mathf.Max(0.0001f, mass);
            drag = Mathf.Max(0f, drag);
            angularDrag = Mathf.Max(0f, angularDrag);
            sleepThreshold = Mathf.Max(0f, sleepThreshold);
            maximumLinearSpeed = Mathf.Max(0.01f, maximumLinearSpeed);
            maximumAngularSpeed = Mathf.Max(0.01f, maximumAngularSpeed);
        }
#endif
    }
}

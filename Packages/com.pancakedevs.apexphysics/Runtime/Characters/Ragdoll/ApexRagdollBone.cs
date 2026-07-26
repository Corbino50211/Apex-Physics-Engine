using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Connects one physical ragdoll bone to a matching animated target bone.
    /// The physical hierarchy and target hierarchy should have matching local axes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public sealed class ApexRagdollBone : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool rootBone;
        [SerializeField, Range(0f, 2f)] private float muscleMultiplier = 1f;

        private Rigidbody cachedRigidbody;
        private ConfigurableJoint cachedJoint;
        private Quaternion startingLocalRotation;
        private bool poseCaptured;

        public event Action<ApexRagdollBone, Collision> Impacted;

        public Transform Target => target;
        public Rigidbody Body
        {
            get
            {
                CacheReferences();
                return cachedRigidbody;
            }
        }

        public ConfigurableJoint Joint
        {
            get
            {
                CacheReferences();
                return cachedJoint;
            }
        }

        public bool IsRootBone => rootBone;
        public float MuscleMultiplier => muscleMultiplier;

        private void Reset()
        {
            CacheReferences();
            PrepareJoint();
            CapturePose();
        }

        private void Awake()
        {
            CacheReferences();
            PrepareJoint();
            CapturePose();
        }

        private void OnEnable()
        {
            CacheReferences();
            PrepareJoint();
        }

        public void Configure(Transform newTarget, bool isRoot)
        {
            target = newTarget;
            rootBone = isRoot;
            CapturePose();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            CapturePose();
        }

        public void SetMuscleMultiplier(float multiplier)
        {
            muscleMultiplier = Mathf.Clamp(multiplier, 0f, 2f);
        }

        public void CapturePose()
        {
            startingLocalRotation = transform.localRotation;
            poseCaptured = true;
        }

        public void ApplyMuscle(ApexRagdollProfile profile, float strength)
        {
            CacheReferences();
            if (profile == null || cachedJoint == null || target == null)
            {
                DisableMuscle();
                return;
            }

            if (!poseCaptured)
            {
                CapturePose();
            }

            float clampedStrength = Mathf.Clamp01(strength) * Mathf.Max(0f, muscleMultiplier);
            JointDrive drive = cachedJoint.slerpDrive;
            drive.positionSpring = profile.MuscleSpring * clampedStrength;
            drive.positionDamper = profile.MuscleDamper * clampedStrength;
            drive.maximumForce = profile.MaximumMuscleForce * clampedStrength;

            cachedJoint.rotationDriveMode = RotationDriveMode.Slerp;
            cachedJoint.slerpDrive = drive;
            cachedJoint.targetAngularVelocity = Vector3.zero;
            cachedJoint.targetRotation = CalculateTargetRotationLocal(
                cachedJoint,
                target.localRotation,
                startingLocalRotation);

            if (cachedRigidbody != null)
            {
                cachedRigidbody.maxAngularVelocity = profile.MaximumAngularVelocity;
            }
        }

        public void DisableMuscle()
        {
            CacheReferences();
            if (cachedJoint == null)
            {
                return;
            }

            JointDrive drive = cachedJoint.slerpDrive;
            drive.positionSpring = 0f;
            drive.positionDamper = 0f;
            drive.maximumForce = 0f;
            cachedJoint.slerpDrive = drive;
        }

        private void PrepareJoint()
        {
            if (cachedJoint == null)
            {
                return;
            }

            cachedJoint.configuredInWorldSpace = false;
            cachedJoint.rotationDriveMode = RotationDriveMode.Slerp;
        }

        private void CacheReferences()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            if (cachedJoint == null)
            {
                cachedJoint = GetComponent<ConfigurableJoint>();
            }
        }

        private static Quaternion CalculateTargetRotationLocal(
            ConfigurableJoint joint,
            Quaternion targetLocalRotation,
            Quaternion startingLocalRotation)
        {
            Vector3 right = joint.axis.normalized;
            Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 up = Vector3.Cross(forward, right).normalized;
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }

            Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);
            Quaternion result = Quaternion.Inverse(worldToJointSpace);
            result *= Quaternion.Inverse(targetLocalRotation) * startingLocalRotation;
            result *= worldToJointSpace;
            return result;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null)
            {
                Impacted?.Invoke(this, collision);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            muscleMultiplier = Mathf.Clamp(muscleMultiplier, 0f, 2f);
            CacheReferences();
            PrepareJoint();
        }
#endif
    }
}

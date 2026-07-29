using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    [CreateAssetMenu(
        fileName = "Apex Ragdoll Profile",
        menuName = "PancakeDevs/Apex Physics/Ragdoll Profile")]
    public sealed class ApexRagdollProfile : ScriptableObject
    {
        [Header("Muscles")]
        [SerializeField, Min(0f)] private float muscleSpring = 1200f;
        [SerializeField, Min(0f)] private float muscleDamper = 80f;
        [SerializeField, Min(0f)] private float maximumMuscleForce = 5000f;
        [SerializeField, Min(0f)] private float maximumAngularVelocity = 40f;

        [Header("Root Following")]
        [SerializeField, Min(0f)] private float rootPositionSpring = 900f;
        [SerializeField, Min(0f)] private float rootPositionDamper = 80f;
        [SerializeField, Min(0f)] private float maximumRootForce = 5000f;
        [SerializeField, Min(0f)] private float rootRotationSpring = 500f;
        [SerializeField, Min(0f)] private float rootRotationDamper = 50f;
        [SerializeField, Min(0f)] private float maximumRootTorque = 2500f;

        [Header("Knockdown")]
        [SerializeField, Min(0f)] private float knockdownImpactSpeed = 6f;
        [SerializeField, Min(0f)] private float knockdownImpactImpulse = 4f;
        [SerializeField] private bool automaticallyRecover = true;
        [SerializeField, Min(0f)] private float limpDuration = 2f;
        [SerializeField, Min(0.01f)] private float recoveryBlendDuration = 1.5f;

        public float MuscleSpring => muscleSpring;
        public float MuscleDamper => muscleDamper;
        public float MaximumMuscleForce => maximumMuscleForce;
        public float MaximumAngularVelocity => maximumAngularVelocity;
        public float RootPositionSpring => rootPositionSpring;
        public float RootPositionDamper => rootPositionDamper;
        public float MaximumRootForce => maximumRootForce;
        public float RootRotationSpring => rootRotationSpring;
        public float RootRotationDamper => rootRotationDamper;
        public float MaximumRootTorque => maximumRootTorque;
        public float KnockdownImpactSpeed => knockdownImpactSpeed;
        public float KnockdownImpactImpulse => knockdownImpactImpulse;
        public bool AutomaticallyRecover => automaticallyRecover;
        public float LimpDuration => limpDuration;
        public float RecoveryBlendDuration => recoveryBlendDuration;

#if UNITY_EDITOR
        private void OnValidate()
        {
            muscleSpring = Mathf.Max(0f, muscleSpring);
            muscleDamper = Mathf.Max(0f, muscleDamper);
            maximumMuscleForce = Mathf.Max(0f, maximumMuscleForce);
            maximumAngularVelocity = Mathf.Max(0f, maximumAngularVelocity);
            rootPositionSpring = Mathf.Max(0f, rootPositionSpring);
            rootPositionDamper = Mathf.Max(0f, rootPositionDamper);
            maximumRootForce = Mathf.Max(0f, maximumRootForce);
            rootRotationSpring = Mathf.Max(0f, rootRotationSpring);
            rootRotationDamper = Mathf.Max(0f, rootRotationDamper);
            maximumRootTorque = Mathf.Max(0f, maximumRootTorque);
            knockdownImpactSpeed = Mathf.Max(0f, knockdownImpactSpeed);
            knockdownImpactImpulse = Mathf.Max(0f, knockdownImpactImpulse);
            limpDuration = Mathf.Max(0f, limpDuration);
            recoveryBlendDuration = Mathf.Max(0.01f, recoveryBlendDuration);
        }
#endif
    }
}

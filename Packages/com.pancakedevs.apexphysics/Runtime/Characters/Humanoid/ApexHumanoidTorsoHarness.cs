using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Force-driven chest support for a locoball humanoid. The simplified support body
    /// carries the torso through a generated chest anchor while the articulated spine,
    /// shoulders, arms, neck, and head remain physical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidTorsoHarness : MonoBehaviour
    {
        [Header("Generated Harness")]
        [SerializeField] private Transform chestAnchor;
        [SerializeField] private Transform chestBone;
        [SerializeField] private Rigidbody chestBody;

        [Header("Humanoid Systems")]
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private ApexHumanoidSupportRig supportRig;
        [SerializeField] private Rigidbody supportBody;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;

        [Header("Position Support")]
        [SerializeField, Min(0f)] private float positionSpring = 3200f;
        [SerializeField, Min(0f)] private float positionDamper = 220f;
        [SerializeField, Min(0f)] private float maximumPositionForce = 8500f;
        [SerializeField, Min(0f)] private float emergencyDistanceRatio = 0.32f;
        [SerializeField, Min(1f)] private float emergencyForceMultiplier = 2f;

        [Header("Rotation Support")]
        [SerializeField, Min(0f)] private float rotationSpring = 1800f;
        [SerializeField, Min(0f)] private float rotationDamper = 140f;
        [SerializeField, Min(0f)] private float maximumRotationTorque = 5000f;

        [Header("Standing Pose")]
        [SerializeField] private bool standingPoseCaptured;
        [SerializeField] private Vector3 standingChestLocalPosition;
        [SerializeField] private Quaternion standingChestLocalRotation = Quaternion.identity;

        private float characterHeight = 1.8f;

        public Transform ChestAnchor => chestAnchor;
        public Transform ChestBone => chestBone;
        public Rigidbody ChestBody => chestBody;
        public bool IsReady => chestAnchor != null && chestBody != null && supportBody != null;

        private void Awake()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            Configure(humanoid);
        }

        private void FixedUpdate()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            float strength = GetControlStrength();
            if (strength <= 0f)
            {
                return;
            }

            ApplyPositionSupport(strength);
            ApplyRotationSupport(strength);
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            humanoid = owner;
            supportRig = owner != null ? owner.SupportRig : GetComponent<ApexHumanoidSupportRig>();
            if (supportRig == null)
            {
                supportRig = GetComponent<ApexHumanoidSupportRig>();
            }

            supportBody = supportRig != null ? supportRig.SupportBody : null;
            activeRagdoll = owner != null ? owner.ActiveRagdoll : null;

            ResolveChestBody();
            BuildOrRepairAnchor(false);
        }

        public void RebuildHarness()
        {
            standingPoseCaptured = false;
            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
            BuildOrRepairAnchor(true);
        }

        public void SnapChestToHarness()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            chestBody.position = chestAnchor.position;
            chestBody.rotation = chestAnchor.rotation;
            chestBody.velocity = supportBody.GetPointVelocity(chestAnchor.position);
            chestBody.angularVelocity = supportBody.angularVelocity;
        }

        private bool ResolveRequiredReferences()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            if (supportRig == null && humanoid != null)
            {
                supportRig = humanoid.SupportRig != null
                    ? humanoid.SupportRig
                    : GetComponent<ApexHumanoidSupportRig>();
            }

            if (supportBody == null && supportRig != null)
            {
                supportBody = supportRig.SupportBody;
            }

            if (activeRagdoll == null && humanoid != null)
            {
                activeRagdoll = humanoid.ActiveRagdoll;
            }

            if (chestBody == null || chestBone == null)
            {
                ResolveChestBody();
            }

            if (chestAnchor == null && supportBody != null)
            {
                BuildOrRepairAnchor(false);
            }

            return chestAnchor != null && chestBody != null && supportBody != null;
        }

        private void ResolveChestBody()
        {
            chestBone = null;
            chestBody = null;

            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            Animator animator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chestBone == null)
            {
                chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            if (chestBone == null)
            {
                chestBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (chestBone != null)
            {
                chestBody = chestBone.GetComponent<Rigidbody>();
            }
        }

        private void BuildOrRepairAnchor(bool forceCapture)
        {
            if (supportBody == null || chestBone == null)
            {
                return;
            }

            characterHeight = supportRig != null
                ? Mathf.Max(0.5f, supportRig.CharacterHeight)
                : 1.8f;

            Transform existing = supportBody.transform.Find("Chest Harness Anchor");
            if (existing == null)
            {
                GameObject anchorObject = new GameObject("Chest Harness Anchor");
                existing = anchorObject.transform;
                existing.SetParent(supportBody.transform, false);
            }

            chestAnchor = existing;
            chestAnchor.localScale = Vector3.one;

            if (forceCapture || !standingPoseCaptured)
            {
                standingChestLocalPosition = supportBody.transform.InverseTransformPoint(chestBone.position);
                standingChestLocalRotation = Quaternion.Inverse(supportBody.rotation) * chestBone.rotation;
                standingPoseCaptured = true;
            }

            chestAnchor.localPosition = standingChestLocalPosition;
            chestAnchor.localRotation = standingChestLocalRotation;
        }

        private void ApplyPositionSupport(float strength)
        {
            Vector3 targetPosition = chestAnchor.position;
            Vector3 targetVelocity = supportBody.GetPointVelocity(targetPosition);
            Vector3 positionError = targetPosition - chestBody.position;
            Vector3 velocityError = targetVelocity - chestBody.velocity;

            float forceMultiplier = positionError.magnitude > characterHeight * emergencyDistanceRatio
                ? emergencyForceMultiplier
                : 1f;

            Vector3 force = positionError * positionSpring + velocityError * positionDamper;
            force = Vector3.ClampMagnitude(
                force,
                maximumPositionForce * forceMultiplier) * strength;
            chestBody.AddForce(force, ForceMode.Force);
        }

        private void ApplyRotationSupport(float strength)
        {
            Quaternion delta = chestAnchor.rotation * Quaternion.Inverse(chestBody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (axis.sqrMagnitude < 0.0001f || float.IsNaN(axis.x))
            {
                return;
            }

            Vector3 relativeAngularVelocity = chestBody.angularVelocity - supportBody.angularVelocity;
            Vector3 torque = axis.normalized *
                             (angleDegrees * Mathf.Deg2Rad * rotationSpring) -
                             relativeAngularVelocity * rotationDamper;
            torque = Vector3.ClampMagnitude(torque, maximumRotationTorque) * strength;
            chestBody.AddTorque(torque, ForceMode.Force);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            positionSpring = Mathf.Max(0f, positionSpring);
            positionDamper = Mathf.Max(0f, positionDamper);
            maximumPositionForce = Mathf.Max(0f, maximumPositionForce);
            emergencyDistanceRatio = Mathf.Max(0f, emergencyDistanceRatio);
            emergencyForceMultiplier = Mathf.Max(1f, emergencyForceMultiplier);
            rotationSpring = Mathf.Max(0f, rotationSpring);
            rotationDamper = Mathf.Max(0f, rotationDamper);
            maximumRotationTorque = Mathf.Max(0f, maximumRotationTorque);
        }

        private void OnDrawGizmosSelected()
        {
            if (chestAnchor == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(chestAnchor.position, Mathf.Max(0.025f, characterHeight * 0.035f));
            if (chestBody != null)
            {
                Gizmos.DrawLine(chestAnchor.position, chestBody.position);
            }
        }
#endif
    }
}

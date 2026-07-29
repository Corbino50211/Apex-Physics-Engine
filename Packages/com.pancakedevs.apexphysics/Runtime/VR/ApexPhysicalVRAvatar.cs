using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Drives a Humanoid avatar from an Apex physical OpenXR body. The Rigidbody body
    /// remains authoritative while the visible avatar follows the body, headset, and
    /// physical hand transforms.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class ApexPhysicalVRAvatar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ApexPhysicalOpenXRBody physicalBody;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;

        [Header("Body Alignment")]
        [SerializeField, Range(0f, 1f)] private float bodyYawFollow = 0.35f;
        [SerializeField, Min(0f)] private float maximumRootCorrectionPerFrame = 0.25f;
        [SerializeField] private Vector3 avatarRootOffset;

        [Header("Arm IK")]
        [SerializeField] private Vector3 leftElbowPoleLocal = new Vector3(-0.45f, -0.15f, 0.25f);
        [SerializeField] private Vector3 rightElbowPoleLocal = new Vector3(0.45f, -0.15f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float handRotationWeight = 1f;

        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform head;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Quaternion hipsBindLocalRotation;
        private Quaternion spineBindLocalRotation;
        private Quaternion chestBindLocalRotation;
        private float leftUpperLength;
        private float leftLowerLength;
        private float rightUpperLength;
        private float rightLowerLength;

        public Animator Animator => animator;
        public ApexPhysicalOpenXRBody PhysicalBody => physicalBody;

        private void Awake()
        {
            CacheBones();
        }

        private void OnEnable()
        {
            CacheBones();
        }

        private void LateUpdate()
        {
            if (!IsReady())
            {
                return;
            }

            AlignAvatarRoot();
            AlignTorso();
            SolveArm(leftUpperArm, leftLowerArm, leftHand, leftHandTarget,
                transform.TransformPoint(leftElbowPoleLocal), leftUpperLength, leftLowerLength);
            SolveArm(rightUpperArm, rightLowerArm, rightHand, rightHandTarget,
                transform.TransformPoint(rightElbowPoleLocal), rightUpperLength, rightLowerLength);

            if (head != null && headTarget != null)
            {
                head.rotation = headTarget.rotation;
            }
        }

        private void CacheBones()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null)
            {
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            head = animator.GetBoneTransform(HumanBodyBones.Head);

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (hips != null)
            {
                hipsBindLocalRotation = hips.localRotation;
            }
            if (spine != null)
            {
                spineBindLocalRotation = spine.localRotation;
            }
            if (chest != null)
            {
                chestBindLocalRotation = chest.localRotation;
            }

            leftUpperLength = BoneLength(leftUpperArm, leftLowerArm);
            leftLowerLength = BoneLength(leftLowerArm, leftHand);
            rightUpperLength = BoneLength(rightUpperArm, rightLowerArm);
            rightLowerLength = BoneLength(rightLowerArm, rightHand);
        }

        private bool IsReady()
        {
            return animator != null && animator.isHuman && physicalBody != null &&
                   headTarget != null && leftHandTarget != null && rightHandTarget != null;
        }

        private void AlignAvatarRoot()
        {
            Vector3 bodyPosition = physicalBody.transform.TransformPoint(avatarRootOffset);
            transform.position = bodyPosition;

            Vector3 forward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetYaw,
                    Mathf.Clamp01(bodyYawFollow));
            }

            if (head != null)
            {
                Vector3 correction = headTarget.position - head.position;
                correction = Vector3.ClampMagnitude(correction, maximumRootCorrectionPerFrame);
                transform.position += correction;
            }
        }

        private void AlignTorso()
        {
            if (hips != null)
            {
                hips.localRotation = hipsBindLocalRotation;
            }
            if (spine != null)
            {
                spine.localRotation = spineBindLocalRotation;
            }
            if (chest != null)
            {
                chest.localRotation = chestBindLocalRotation;
            }
        }

        private void SolveArm(
            Transform upper,
            Transform lower,
            Transform handBone,
            Transform target,
            Vector3 pole,
            float upperLength,
            float lowerLength)
        {
            if (upper == null || lower == null || handBone == null || target == null ||
                upperLength <= 0f || lowerLength <= 0f)
            {
                return;
            }

            Vector3 shoulder = upper.position;
            Vector3 toTarget = target.position - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, 0.001f, upperLength + lowerLength - 0.001f);
            Vector3 direction = toTarget.normalized;

            Vector3 poleDirection = pole - shoulder;
            Vector3 bendNormal = Vector3.Cross(direction, poleDirection).normalized;
            if (bendNormal.sqrMagnitude < 0.0001f)
            {
                bendNormal = Vector3.Cross(direction, transform.up).normalized;
            }
            Vector3 bendDirection = Vector3.Cross(bendNormal, direction).normalized;

            float shoulderAngle = Mathf.Acos(Mathf.Clamp(
                (upperLength * upperLength + distance * distance - lowerLength * lowerLength) /
                (2f * upperLength * distance), -1f, 1f));

            Vector3 elbowPosition = shoulder +
                                    direction * (Mathf.Cos(shoulderAngle) * upperLength) +
                                    bendDirection * (Mathf.Sin(shoulderAngle) * upperLength);

            RotateBoneToward(upper, lower.position, elbowPosition);
            RotateBoneToward(lower, handBone.position, target.position);
            handBone.rotation = Quaternion.Slerp(handBone.rotation, target.rotation, handRotationWeight);
        }

        private static void RotateBoneToward(Transform bone, Vector3 currentChildPosition, Vector3 desiredChildPosition)
        {
            Vector3 currentDirection = currentChildPosition - bone.position;
            Vector3 desiredDirection = desiredChildPosition - bone.position;
            if (currentDirection.sqrMagnitude < 0.000001f || desiredDirection.sqrMagnitude < 0.000001f)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(currentDirection, desiredDirection) * bone.rotation;
        }

        private static float BoneLength(Transform from, Transform to)
        {
            return from != null && to != null ? Vector3.Distance(from.position, to.position) : 0f;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Animator humanoidAnimator,
            ApexPhysicalOpenXRBody body,
            Transform trackedHead,
            Transform physicalLeftHand,
            Transform physicalRightHand)
        {
            animator = humanoidAnimator;
            physicalBody = body;
            headTarget = trackedHead;
            leftHandTarget = physicalLeftHand;
            rightHandTarget = physicalRightHand;
            CacheBones();
        }
#endif
    }
}

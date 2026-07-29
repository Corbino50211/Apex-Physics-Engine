using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Applies external head and hand tracking poses to a hidden humanoid target skeleton.
    /// The active ragdoll then physically follows those target bones.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidTrackingDriver : MonoBehaviour
    {
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;

        [Header("Tracking Weights")]
        [SerializeField, Range(0f, 1f)] private float headPositionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float headRotationWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float handPositionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float handRotationWeight = 1f;

        private Transform targetHead;
        private Transform targetLeftHand;
        private Transform targetRightHand;

        public Animator TargetAnimator => targetAnimator;
        public Transform HeadTarget => headTarget;
        public Transform LeftHandTarget => leftHandTarget;
        public Transform RightHandTarget => rightHandTarget;

        public void Configure(
            Animator animator,
            Transform newHeadTarget,
            Transform newLeftHandTarget,
            Transform newRightHandTarget)
        {
            targetAnimator = animator;
            headTarget = newHeadTarget;
            leftHandTarget = newLeftHandTarget;
            rightHandTarget = newRightHandTarget;
            RefreshBones();
        }

        public void SetTrackingTargets(
            Transform newHeadTarget,
            Transform newLeftHandTarget,
            Transform newRightHandTarget)
        {
            headTarget = newHeadTarget;
            leftHandTarget = newLeftHandTarget;
            rightHandTarget = newRightHandTarget;
        }

        public void RefreshBones()
        {
            targetHead = null;
            targetLeftHand = null;
            targetRightHand = null;

            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                return;
            }

            targetHead = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
            targetLeftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            targetRightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void LateUpdate()
        {
            if (targetAnimator == null)
            {
                return;
            }

            if (targetHead == null && targetLeftHand == null && targetRightHand == null)
            {
                RefreshBones();
            }

            ApplyPose(targetHead, headTarget, headPositionWeight, headRotationWeight);
            ApplyPose(targetLeftHand, leftHandTarget, handPositionWeight, handRotationWeight);
            ApplyPose(targetRightHand, rightHandTarget, handPositionWeight, handRotationWeight);
        }

        private static void ApplyPose(
            Transform bone,
            Transform target,
            float positionWeight,
            float rotationWeight)
        {
            if (bone == null || target == null)
            {
                return;
            }

            if (positionWeight > 0f)
            {
                bone.position = Vector3.Lerp(bone.position, target.position, positionWeight);
            }

            if (rotationWeight > 0f)
            {
                bone.rotation = Quaternion.Slerp(bone.rotation, target.rotation, rotationWeight);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            headPositionWeight = Mathf.Clamp01(headPositionWeight);
            headRotationWeight = Mathf.Clamp01(headRotationWeight);
            handPositionWeight = Mathf.Clamp01(handPositionWeight);
            handRotationWeight = Mathf.Clamp01(handRotationWeight);
            RefreshBones();
        }
#endif
    }
}

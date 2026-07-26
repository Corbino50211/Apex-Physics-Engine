using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Keeps the hidden animated target skeleton aligned with a physical root while
    /// preserving the Humanoid hips bone's authored position and rotation offsets.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidTargetRootDriver : MonoBehaviour
    {
        [SerializeField] private Transform physicalRoot;
        [SerializeField] private Transform targetRoot;
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followRotation = true;

        [Header("Preserved Target Offset")]
        [SerializeField] private bool offsetCaptured;
        [SerializeField] private Vector3 targetLocalPositionOffset;
        [SerializeField] private Quaternion targetLocalRotationOffset = Quaternion.identity;

        public Transform PhysicalRoot => physicalRoot;
        public Transform TargetRoot => targetRoot;
        public bool OffsetCaptured => offsetCaptured;
        public Vector3 TargetLocalPositionOffset => targetLocalPositionOffset;
        public Quaternion TargetLocalRotationOffset => targetLocalRotationOffset;

        public void Configure(
            Transform newPhysicalRoot,
            Transform newTargetRoot,
            bool shouldFollowPosition = true,
            bool shouldFollowRotation = true,
            bool preserveCurrentOffset = true)
        {
            bool sameRelationship = physicalRoot == newPhysicalRoot && targetRoot == newTargetRoot;

            physicalRoot = newPhysicalRoot;
            targetRoot = newTargetRoot;
            followPosition = shouldFollowPosition;
            followRotation = shouldFollowRotation;

            if (preserveCurrentOffset)
            {
                // Rebuilding or recovering the same rig must not capture the temporary
                // ragdoll separation as a new standing offset.
                if (!sameRelationship || !offsetCaptured)
                {
                    CaptureCurrentOffset();
                }
            }
            else
            {
                targetLocalPositionOffset = Vector3.zero;
                targetLocalRotationOffset = Quaternion.identity;
                offsetCaptured = true;
            }

            SnapNow();
        }

        /// <summary>
        /// Captures the target root's current pose relative to the physical root.
        /// Humanoid hips commonly have a non-identity authored rotation, so this
        /// offset must be retained instead of forcing both transforms to match.
        /// </summary>
        public void CaptureCurrentOffset()
        {
            if (physicalRoot == null || targetRoot == null)
            {
                offsetCaptured = false;
                return;
            }

            targetLocalPositionOffset = physicalRoot.InverseTransformPoint(targetRoot.position);
            targetLocalRotationOffset = Quaternion.Inverse(physicalRoot.rotation) * targetRoot.rotation;
            offsetCaptured = true;
        }

        public void ResetToZeroOffset()
        {
            targetLocalPositionOffset = Vector3.zero;
            targetLocalRotationOffset = Quaternion.identity;
            offsetCaptured = true;
            SnapNow();
        }

        public void SnapNow()
        {
            if (physicalRoot == null || targetRoot == null)
            {
                return;
            }

            if (!offsetCaptured)
            {
                CaptureCurrentOffset();
            }

            if (followPosition)
            {
                targetRoot.position = physicalRoot.TransformPoint(targetLocalPositionOffset);
            }

            if (followRotation)
            {
                targetRoot.rotation = physicalRoot.rotation * targetLocalRotationOffset;
            }
        }

        private void LateUpdate()
        {
            SnapNow();
        }
    }
}

using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Keeps the hidden animated target skeleton aligned with the physical hips.
    /// Animation continues to pose the target limbs while the physical root owns locomotion.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidTargetRootDriver : MonoBehaviour
    {
        [SerializeField] private Transform physicalRoot;
        [SerializeField] private Transform targetRoot;
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followRotation = true;

        public Transform PhysicalRoot => physicalRoot;
        public Transform TargetRoot => targetRoot;

        public void Configure(
            Transform newPhysicalRoot,
            Transform newTargetRoot,
            bool shouldFollowPosition = true,
            bool shouldFollowRotation = true)
        {
            physicalRoot = newPhysicalRoot;
            targetRoot = newTargetRoot;
            followPosition = shouldFollowPosition;
            followRotation = shouldFollowRotation;
            SnapNow();
        }

        public void SnapNow()
        {
            if (physicalRoot == null || targetRoot == null)
            {
                return;
            }

            if (followPosition)
            {
                targetRoot.position = physicalRoot.position;
            }

            if (followRotation)
            {
                targetRoot.rotation = physicalRoot.rotation;
            }
        }

        private void LateUpdate()
        {
            SnapNow();
        }
    }
}

using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>Optional authored handle or grip position on an ApexGrabbable.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexGrabPoint : MonoBehaviour
    {
        [SerializeField] private ApexHandedness allowedHand = ApexHandedness.Any;
        [SerializeField] private bool followRotation = true;
        [SerializeField, Min(0.01f)] private float maximumGrabDistance = 0.35f;
        [SerializeField] private int priority;

        public ApexHandedness AllowedHand => allowedHand;
        public bool FollowRotation => followRotation;
        public float MaximumGrabDistance => maximumGrabDistance;
        public int Priority => priority;

        public bool CanBeUsedBy(ApexHandedness handedness, float distance)
        {
            bool handMatches = allowedHand == ApexHandedness.Any ||
                               handedness == ApexHandedness.Any ||
                               allowedHand == handedness;

            return handMatches && distance <= maximumGrabDistance;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumGrabDistance = Mathf.Max(0.01f, maximumGrabDistance);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, maximumGrabDistance);
            Gizmos.DrawRay(transform.position, transform.forward * 0.12f);
            Gizmos.DrawRay(transform.position, transform.up * 0.08f);
        }
#endif
    }
}

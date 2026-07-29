using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Preserves each Humanoid model's native foot-bone orientation while aligning its
    /// feet to the floor. This corrects models whose foot bone axes do not use Unity +Z.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class ApexVRFootOrientationCorrection : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.05f)] private float rayHeight = 0.35f;
        [SerializeField, Min(0.1f)] private float rayDistance = 0.8f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;

        private Transform leftFoot;
        private Transform rightFoot;
        private Quaternion leftBindOffset = Quaternion.identity;
        private Quaternion rightBindOffset = Quaternion.identity;
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (!initialized)
            {
                return;
            }

            CorrectFoot(leftFoot, leftBindOffset);
            CorrectFoot(rightFoot, rightBindOffset);
        }

        private void Initialize()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (animator == null || !animator.isHuman)
            {
                initialized = false;
                return;
            }

            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                initialized = false;
                return;
            }

            Quaternion reference = GetReferenceRotation(Vector3.up);
            leftBindOffset = Quaternion.Inverse(reference) * leftFoot.rotation;
            rightBindOffset = Quaternion.Inverse(reference) * rightFoot.rotation;
            initialized = true;
        }

        private void CorrectFoot(Transform foot, Quaternion bindOffset)
        {
            Vector3 normal = Vector3.up;
            Vector3 origin = foot.position + Vector3.up * rayHeight;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    rayDistance,
                    groundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
            }

            Quaternion target = GetReferenceRotation(normal) * bindOffset;
            foot.rotation = Quaternion.Slerp(foot.rotation, target, rotationWeight);
        }

        private Quaternion GetReferenceRotation(Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            }

            return Quaternion.LookRotation(forward, up);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnExistingAvatars()
        {
            ApexPhysicalVRAvatar[] avatars = Object.FindObjectsByType<ApexPhysicalVRAvatar>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ApexPhysicalVRAvatar avatar in avatars)
            {
                if (avatar != null && avatar.GetComponent<ApexVRFootOrientationCorrection>() == null)
                {
                    avatar.gameObject.AddComponent<ApexVRFootOrientationCorrection>();
                }
            }
        }
    }
}

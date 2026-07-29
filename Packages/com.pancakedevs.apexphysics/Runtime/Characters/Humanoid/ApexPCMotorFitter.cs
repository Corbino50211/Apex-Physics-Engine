using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Keeps the PC character motor embedded around the humanoid pelvis and torso while
    /// preserving floor contact. The motor Rigidbody pivot is placed at the hips instead
    /// of on the ground, and the capsule center is offset so its bottom remains on the floor.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ApexPCMotorFitter : MonoBehaviour
    {
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private ApexPCPhysicalCharacter character;

        [Header("Motor Fit")]
        [SerializeField, Range(0.45f, 0.9f)] private float capsuleHeightRatio = 0.72f;
        [SerializeField, Range(0.08f, 0.3f)] private float capsuleRadiusRatio = 0.14f;
        [SerializeField, Min(0f)] private float floorClearance = 0.02f;
        [SerializeField] private bool fitOnStart = true;

        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private bool subscribed;

        public ApexPhysicalHumanoid Humanoid => humanoid;
        public ApexPCPhysicalCharacter Character => character;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            if (fitOnStart)
            {
                FitNow();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            Unsubscribe();

            humanoid = owner;
            character = owner != null
                ? (owner.PCPhysicalCharacter != null
                    ? owner.PCPhysicalCharacter
                    : owner.GetComponent<ApexPCPhysicalCharacter>())
                : GetComponent<ApexPCPhysicalCharacter>();

            Subscribe();
            FitNow();
        }

        public bool FitNow()
        {
            ResolveReferences();
            if (humanoid == null || character == null || character.MotorBody == null ||
                character.MotorCollider == null || humanoid.PhysicalHips == null)
            {
                return false;
            }

            Rigidbody motorBody = character.MotorBody;
            CapsuleCollider motorCollider = character.MotorCollider;
            Transform motorTransform = motorBody.transform;
            Transform physicalHips = humanoid.PhysicalHips.transform;
            Transform targetHips = humanoid.TargetHips;
            Transform hipsAnchor = motorTransform.Find("Hips Anchor");

            float characterHeight = CalculateCharacterHeight();
            float groundHeight = ResolveGroundHeight(physicalHips.position, characterHeight);

            Vector3 forward = Vector3.ProjectOnPlane(physicalHips.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(motorTransform.forward, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            Quaternion uprightRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 motorPosition = physicalHips.position;

            if (Application.isPlaying)
            {
                motorBody.position = motorPosition;
                motorBody.rotation = uprightRotation;
                motorBody.velocity = Vector3.zero;
                motorBody.angularVelocity = Vector3.zero;
            }
            else
            {
                motorTransform.SetPositionAndRotation(motorPosition, uprightRotation);
            }

            motorTransform.localScale = Vector3.one;

            float radius = Mathf.Clamp(characterHeight * capsuleRadiusRatio, 0.12f, 0.42f);
            float height = Mathf.Max(radius * 2f, characterHeight * capsuleHeightRatio);
            float centerWorldY = groundHeight + floorClearance + height * 0.5f;
            Vector3 worldCenter = new Vector3(motorPosition.x, centerWorldY, motorPosition.z);

            motorCollider.direction = 1;
            motorCollider.radius = radius;
            motorCollider.height = height;
            motorCollider.center = motorTransform.InverseTransformPoint(worldCenter);

            motorBody.ResetCenterOfMass();
            motorBody.ResetInertiaTensor();

            if (hipsAnchor != null)
            {
                Vector3 anchorPosition = targetHips != null ? targetHips.position : physicalHips.position;
                Quaternion anchorRotation = targetHips != null ? targetHips.rotation : physicalHips.rotation;
                hipsAnchor.SetPositionAndRotation(anchorPosition, anchorRotation);
                hipsAnchor.localScale = Vector3.one;

                ApexHumanoidTargetRootDriver targetDriver = humanoid.TargetRootDriver;
                if (targetDriver != null && targetHips != null)
                {
                    targetDriver.Configure(hipsAnchor, targetHips, true, true, false);
                    targetDriver.SnapNow();
                }
            }

            Physics.SyncTransforms();
            return true;
        }

        private void HandleCharacterStateChanged(ApexPCCharacterState state)
        {
            if (state == ApexPCCharacterState.Active)
            {
                FitNow();
            }
        }

        private void Subscribe()
        {
            if (subscribed || character == null)
            {
                return;
            }

            character.StateChanged += HandleCharacterStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || character == null)
            {
                subscribed = false;
                return;
            }

            character.StateChanged -= HandleCharacterStateChanged;
            subscribed = false;
        }

        private void ResolveReferences()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            if (character == null)
            {
                character = humanoid != null && humanoid.PCPhysicalCharacter != null
                    ? humanoid.PCPhysicalCharacter
                    : GetComponent<ApexPCPhysicalCharacter>();
            }
        }

        private float CalculateCharacterHeight()
        {
            Animator animator = humanoid.PhysicalCharacter != null
                ? humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true)
                : null;
            if (animator == null || !animator.isHuman)
            {
                animator = humanoid.TargetAnimator;
            }
            if (animator == null || !animator.isHuman)
            {
                return 1.8f;
            }

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || (leftFoot == null && rightFoot == null))
            {
                return 1.8f;
            }

            Vector3 feet = leftFoot != null && rightFoot != null
                ? (leftFoot.position + rightFoot.position) * 0.5f
                : (leftFoot != null ? leftFoot.position : rightFoot.position);
            float height = Vector3.Dot(head.position - feet, Vector3.up) * 1.08f;
            return height > 0.5f ? height : 1.8f;
        }

        private float ResolveGroundHeight(Vector3 hipsPosition, float characterHeight)
        {
            float fallback = ResolveFootHeight(hipsPosition, characterHeight);
            Vector3 origin = hipsPosition + Vector3.up * characterHeight;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                characterHeight * 3f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            float result = fallback;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    result = hit.point.y;
                }
            }

            return result;
        }

        private float ResolveFootHeight(Vector3 hipsPosition, float characterHeight)
        {
            Animator animator = humanoid.PhysicalCharacter != null
                ? humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true)
                : null;
            if (animator != null && animator.isHuman)
            {
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot != null && rightFoot != null)
                {
                    return Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                }
                if (leftFoot != null)
                {
                    return leftFoot.position.y;
                }
                if (rightFoot != null)
                {
                    return rightFoot.position.y;
                }
            }

            return hipsPosition.y - characterHeight * 0.52f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            capsuleHeightRatio = Mathf.Clamp(capsuleHeightRatio, 0.45f, 0.9f);
            capsuleRadiusRatio = Mathf.Clamp(capsuleRadiusRatio, 0.08f, 0.3f);
            floorClearance = Mathf.Max(0f, floorClearance);
        }
#endif
    }
}

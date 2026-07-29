using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Drives a Humanoid avatar from an Apex physical OpenXR body. The Rigidbody body
    /// remains authoritative while the visible avatar follows the body, headset, hands,
    /// pelvis, and procedurally planted feet.
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
        [SerializeField, Min(0f)] private float pelvisHeightOffset;

        [Header("Arm IK")]
        [SerializeField] private Vector3 leftElbowPoleLocal = new Vector3(-0.45f, -0.15f, 0.25f);
        [SerializeField] private Vector3 rightElbowPoleLocal = new Vector3(0.45f, -0.15f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float handRotationWeight = 1f;

        [Header("Procedural Legs")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.05f)] private float footSpacing = 0.18f;
        [SerializeField, Min(0.05f)] private float stepDistance = 0.32f;
        [SerializeField, Min(0.01f)] private float stepHeight = 0.12f;
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 0.6f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 1.5f;
        [SerializeField] private float footSurfaceOffset = 0.02f;
        [SerializeField, Range(0f, 1f)] private float footRotationWeight = 1f;
        [SerializeField] private Vector3 leftKneePoleLocal = new Vector3(-0.18f, -0.45f, 0.45f);
        [SerializeField] private Vector3 rightKneePoleLocal = new Vector3(0.18f, -0.45f, 0.45f);

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
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;

        private Quaternion hipsBindLocalRotation;
        private Quaternion spineBindLocalRotation;
        private Quaternion chestBindLocalRotation;
        private float leftUpperLength;
        private float leftLowerLength;
        private float rightUpperLength;
        private float rightLowerLength;
        private float leftThighLength;
        private float leftShinLength;
        private float rightThighLength;
        private float rightShinLength;
        private bool legsInitialized;
        private FootState leftFootState;
        private FootState rightFootState;

        public Animator Animator => animator;
        public ApexPhysicalOpenXRBody PhysicalBody => physicalBody;

        private struct FootState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 StepStartPosition;
            public Quaternion StepStartRotation;
            public Vector3 StepEndPosition;
            public Quaternion StepEndRotation;
            public float StepProgress;
            public bool IsStepping;
        }

        private void Awake()
        {
            CacheBones();
        }

        private void OnEnable()
        {
            CacheBones();
            legsInitialized = false;
        }

        private void LateUpdate()
        {
            if (!IsReady())
            {
                return;
            }

            AlignAvatarRoot();
            AlignTorso();

            SolveTwoBoneChain(
                leftUpperArm,
                leftLowerArm,
                leftHand,
                leftHandTarget.position,
                transform.TransformPoint(leftElbowPoleLocal),
                leftUpperLength,
                leftLowerLength);
            SolveTwoBoneChain(
                rightUpperArm,
                rightLowerArm,
                rightHand,
                rightHandTarget.position,
                transform.TransformPoint(rightElbowPoleLocal),
                rightUpperLength,
                rightLowerLength);

            if (leftHand != null)
            {
                leftHand.rotation = Quaternion.Slerp(leftHand.rotation, leftHandTarget.rotation, handRotationWeight);
            }
            if (rightHand != null)
            {
                rightHand.rotation = Quaternion.Slerp(rightHand.rotation, rightHandTarget.rotation, handRotationWeight);
            }
            if (head != null)
            {
                head.rotation = headTarget.rotation;
            }

            UpdateProceduralLegs();
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

            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

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
            leftThighLength = BoneLength(leftUpperLeg, leftLowerLeg);
            leftShinLength = BoneLength(leftLowerLeg, leftFoot);
            rightThighLength = BoneLength(rightUpperLeg, rightLowerLeg);
            rightShinLength = BoneLength(rightLowerLeg, rightFoot);
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
                Vector3 hipsPosition = hips.position;
                hipsPosition.y = physicalBody.transform.position.y + pelvisHeightOffset +
                                 Mathf.Max(0.35f, headTarget.position.y - physicalBody.transform.position.y) * 0.52f;
                hips.position = hipsPosition;
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

        private void UpdateProceduralLegs()
        {
            if (leftUpperLeg == null || leftLowerLeg == null || leftFoot == null ||
                rightUpperLeg == null || rightLowerLeg == null || rightFoot == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 center = physicalBody.transform.position;

            SampleFootTarget(center - right * footSpacing, forward, out Vector3 leftDesired, out Quaternion leftRotation);
            SampleFootTarget(center + right * footSpacing, forward, out Vector3 rightDesired, out Quaternion rightRotation);

            if (!legsInitialized)
            {
                leftFootState = CreateFootState(leftDesired, leftRotation);
                rightFootState = CreateFootState(rightDesired, rightRotation);
                legsInitialized = true;
            }

            bool leftNeedsStep = !leftFootState.IsStepping &&
                                 Vector3.Distance(leftFootState.Position, leftDesired) > stepDistance;
            bool rightNeedsStep = !rightFootState.IsStepping &&
                                  Vector3.Distance(rightFootState.Position, rightDesired) > stepDistance;

            if (leftNeedsStep && !rightFootState.IsStepping)
            {
                BeginStep(ref leftFootState, leftDesired, leftRotation);
            }
            else if (rightNeedsStep && !leftFootState.IsStepping)
            {
                BeginStep(ref rightFootState, rightDesired, rightRotation);
            }

            AdvanceStep(ref leftFootState);
            AdvanceStep(ref rightFootState);

            SolveTwoBoneChain(
                leftUpperLeg,
                leftLowerLeg,
                leftFoot,
                leftFootState.Position,
                transform.TransformPoint(leftKneePoleLocal),
                leftThighLength,
                leftShinLength);
            SolveTwoBoneChain(
                rightUpperLeg,
                rightLowerLeg,
                rightFoot,
                rightFootState.Position,
                transform.TransformPoint(rightKneePoleLocal),
                rightThighLength,
                rightShinLength);

            leftFoot.rotation = Quaternion.Slerp(leftFoot.rotation, leftFootState.Rotation, footRotationWeight);
            rightFoot.rotation = Quaternion.Slerp(rightFoot.rotation, rightFootState.Rotation, footRotationWeight);
        }

        private void SampleFootTarget(
            Vector3 horizontalPosition,
            Vector3 forward,
            out Vector3 position,
            out Quaternion rotation)
        {
            Vector3 origin = horizontalPosition + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeDistance,
                    groundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                position = hit.point + hit.normal * footSurfaceOffset;
                Vector3 surfaceForward = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
                if (surfaceForward.sqrMagnitude < 0.0001f)
                {
                    surfaceForward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                }
                rotation = Quaternion.LookRotation(surfaceForward, hit.normal);
                return;
            }

            position = horizontalPosition;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private static FootState CreateFootState(Vector3 position, Quaternion rotation)
        {
            return new FootState
            {
                Position = position,
                Rotation = rotation,
                StepStartPosition = position,
                StepStartRotation = rotation,
                StepEndPosition = position,
                StepEndRotation = rotation,
                StepProgress = 1f,
                IsStepping = false
            };
        }

        private void BeginStep(ref FootState state, Vector3 targetPosition, Quaternion targetRotation)
        {
            state.StepStartPosition = state.Position;
            state.StepStartRotation = state.Rotation;
            state.StepEndPosition = targetPosition;
            state.StepEndRotation = targetRotation;
            state.StepProgress = 0f;
            state.IsStepping = true;
        }

        private void AdvanceStep(ref FootState state)
        {
            if (!state.IsStepping)
            {
                return;
            }

            state.StepProgress += Time.deltaTime / Mathf.Max(0.01f, stepDuration);
            float t = Mathf.Clamp01(state.StepProgress);
            Vector3 position = Vector3.Lerp(state.StepStartPosition, state.StepEndPosition, t);
            position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * stepHeight);
            state.Position = position;
            state.Rotation = Quaternion.Slerp(state.StepStartRotation, state.StepEndRotation, t);

            if (t >= 1f)
            {
                state.Position = state.StepEndPosition;
                state.Rotation = state.StepEndRotation;
                state.IsStepping = false;
            }
        }

        private static void SolveTwoBoneChain(
            Transform upper,
            Transform lower,
            Transform end,
            Vector3 targetPosition,
            Vector3 pole,
            float upperLength,
            float lowerLength)
        {
            if (upper == null || lower == null || end == null ||
                upperLength <= 0f || lowerLength <= 0f)
            {
                return;
            }

            Vector3 root = upper.position;
            Vector3 toTarget = targetPosition - root;
            float distance = Mathf.Clamp(toTarget.magnitude, 0.001f, upperLength + lowerLength - 0.001f);
            Vector3 direction = toTarget.normalized;
            Vector3 poleDirection = pole - root;
            Vector3 bendNormal = Vector3.Cross(direction, poleDirection).normalized;
            if (bendNormal.sqrMagnitude < 0.0001f)
            {
                bendNormal = Vector3.Cross(direction, Vector3.up).normalized;
            }
            Vector3 bendDirection = Vector3.Cross(bendNormal, direction).normalized;

            float rootAngle = Mathf.Acos(Mathf.Clamp(
                (upperLength * upperLength + distance * distance - lowerLength * lowerLength) /
                (2f * upperLength * distance), -1f, 1f));

            Vector3 jointPosition = root +
                                    direction * (Mathf.Cos(rootAngle) * upperLength) +
                                    bendDirection * (Mathf.Sin(rootAngle) * upperLength);

            RotateBoneToward(upper, lower.position, jointPosition);
            RotateBoneToward(lower, end.position, targetPosition);
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
            legsInitialized = false;
        }
#endif
    }
}

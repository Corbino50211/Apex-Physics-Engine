using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Supplies a natural Humanoid idle and walking pose when no Animator Controller is
    /// assigned. The component also performs a final knee-pole correction after planted-foot
    /// IK so legs bend forward instead of collapsing inward or trailing behind the motor.
    /// </summary>
    [DefaultExecutionOrder(-270)]
    [DisallowMultipleComponent]
    public sealed class ApexPCProceduralGait : MonoBehaviour
    {
        private const int CurrentSettingsVersion = 1;

        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private bool proceduralWhenNoController = true;
        [SerializeField] private bool forceProceduralGait;

        [Header("Walking Pose")]
        [SerializeField, Min(0.1f)] private float referenceSpeed = 3.5f;
        [SerializeField, Min(0f)] private float stepFrequency = 1.8f;
        [SerializeField, Range(0f, 1f)] private float legSwing = 0.3f;
        [SerializeField, Range(0f, 1f)] private float kneeBend = 0.5f;
        [SerializeField, Range(0f, 1f)] private float footLift = 0.12f;
        [SerializeField, Min(0f)] private float speedBlendRate = 7f;

        [Header("Natural Arms")]
        [SerializeField, Range(0f, 1f)] private float armSwing = 0.28f;
        [SerializeField, Range(0f, 0.35f)] private float armOutward = 0.07f;
        [SerializeField, Range(0.6f, 1.4f)] private float armDownWeight = 1f;

        [Header("Knee Stability")]
        [SerializeField, Range(0f, 1f)] private float kneeForwardBias = 0.92f;
        [SerializeField, Range(0f, 0.75f)] private float kneeOutwardBias = 0.18f;
        [SerializeField, Range(0f, 1f)] private float kneeCorrectionStrength = 1f;
        [SerializeField, HideInInspector] private int settingsVersion;

        private HumanPoseHandler poseHandler;
        private HumanPose pose;
        private float gaitPhase;
        private float smoothedSpeed;

        private int leftUpperLegMuscle = -1;
        private int rightUpperLegMuscle = -1;
        private int leftLowerLegMuscle = -1;
        private int rightLowerLegMuscle = -1;
        private int leftFootMuscle = -1;
        private int rightFootMuscle = -1;

        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform rightUpperArm;
        private Transform rightLowerArm;

        public bool IsUsingProceduralGait => ShouldUseProceduralGait();

        private void Awake()
        {
            ApplyVersionedDefaults();
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ApplyVersionedDefaults();
            CacheHumanoidBones();
        }

        private void OnDestroy()
        {
            DisposePoseHandler();
        }

        private void Update()
        {
            if (!ShouldUseProceduralGait() || character == null || character.MotorBody == null)
            {
                return;
            }

            if (character.State != ApexPCCharacterState.Active &&
                character.State != ApexPCCharacterState.Staggered)
            {
                ResetPose();
                return;
            }

            EnsurePoseHandler();
            if (poseHandler == null)
            {
                return;
            }

            float speed = Vector3.ProjectOnPlane(
                character.MotorBody.velocity,
                Vector3.up).magnitude;
            float targetSpeed = Mathf.Clamp01(speed / Mathf.Max(0.1f, referenceSpeed));
            smoothedSpeed = Mathf.MoveTowards(
                smoothedSpeed,
                targetSpeed,
                speedBlendRate * Time.deltaTime);

            gaitPhase += Time.deltaTime * Mathf.Lerp(0.4f, stepFrequency, smoothedSpeed) *
                         Mathf.PI * 2f;

            poseHandler.GetHumanPose(ref pose);
            float wave = Mathf.Sin(gaitPhase);
            float leftLiftAmount = Mathf.Max(0f, -wave);
            float rightLiftAmount = Mathf.Max(0f, wave);

            SetMuscle(leftUpperLegMuscle, wave * legSwing * smoothedSpeed);
            SetMuscle(rightUpperLegMuscle, -wave * legSwing * smoothedSpeed);
            SetMuscle(leftLowerLegMuscle, -leftLiftAmount * kneeBend * smoothedSpeed);
            SetMuscle(rightLowerLegMuscle, -rightLiftAmount * kneeBend * smoothedSpeed);
            SetMuscle(leftFootMuscle, leftLiftAmount * footLift * smoothedSpeed);
            SetMuscle(rightFootMuscle, rightLiftAmount * footLift * smoothedSpeed);

            poseHandler.SetHumanPose(ref pose);
            ApplyNaturalArmPose(leftUpperArm, leftLowerArm, -1f, -wave);
            ApplyNaturalArmPose(rightUpperArm, rightLowerArm, 1f, wave);
        }

        private void LateUpdate()
        {
            if (!ShouldUseProceduralGait() || character == null || character.MotorBody == null ||
                (character.State != ApexPCCharacterState.Active &&
                 character.State != ApexPCCharacterState.Staggered))
            {
                return;
            }

            // ApexPCFootPlanting runs at -275. This component runs at -270, so the final
            // pole solve happens after foot placement but before ApexPCPhysicalCharacter
            // copies the target pose at -250.
            StabilizeKnee(leftUpperLeg, leftLowerLeg, leftFoot, -1f);
            StabilizeKnee(rightUpperLeg, rightLowerLeg, rightFoot, 1f);
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            ApplyVersionedDefaults();
            character = owner;
            targetAnimator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetAnimator
                : null;
            smoothedSpeed = 0f;
            DisposePoseHandler();
            CacheHumanoidBones();
            EnsurePoseHandler();
        }

        private bool ShouldUseProceduralGait()
        {
            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                return false;
            }

            if (forceProceduralGait)
            {
                return true;
            }

            return proceduralWhenNoController && targetAnimator.runtimeAnimatorController == null;
        }

        private void EnsurePoseHandler()
        {
            if (!ShouldUseProceduralGait() || poseHandler != null || targetAnimator.avatar == null ||
                !targetAnimator.avatar.isValid)
            {
                return;
            }

            poseHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
            pose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };

            leftUpperLegMuscle = FindMuscle("Left Upper Leg Front-Back");
            rightUpperLegMuscle = FindMuscle("Right Upper Leg Front-Back");
            leftLowerLegMuscle = FindMuscle("Left Lower Leg Stretch");
            rightLowerLegMuscle = FindMuscle("Right Lower Leg Stretch");
            leftFootMuscle = FindMuscle("Left Foot Up-Down");
            rightFootMuscle = FindMuscle("Right Foot Up-Down");
        }

        private void CacheHumanoidBones()
        {
            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                ClearBoneCache();
                return;
            }

            leftUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        private void ClearBoneCache()
        {
            leftUpperLeg = null;
            leftLowerLeg = null;
            leftFoot = null;
            rightUpperLeg = null;
            rightLowerLeg = null;
            rightFoot = null;
            leftUpperArm = null;
            leftLowerArm = null;
            rightUpperArm = null;
            rightLowerArm = null;
        }

        private void ApplyNaturalArmPose(
            Transform upperArm,
            Transform lowerArm,
            float sideSign,
            float phase)
        {
            if (upperArm == null || lowerArm == null || character == null ||
                character.MotorBody == null)
            {
                return;
            }

            Transform motor = character.MotorBody.transform;
            Vector3 currentDirection = lowerArm.position - upperArm.position;
            if (currentDirection.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Vector3 desiredDirection =
                Vector3.down * armDownWeight +
                motor.right * (sideSign * armOutward) +
                motor.forward * (phase * armSwing * smoothedSpeed);
            if (desiredDirection.sqrMagnitude < 0.000001f)
            {
                return;
            }

            upperArm.rotation = Quaternion.FromToRotation(
                currentDirection,
                desiredDirection.normalized) * upperArm.rotation;
        }

        private void StabilizeKnee(
            Transform upperLeg,
            Transform lowerLeg,
            Transform foot,
            float sideSign)
        {
            if (upperLeg == null || lowerLeg == null || foot == null || character == null ||
                character.MotorBody == null || kneeCorrectionStrength <= 0f)
            {
                return;
            }

            Vector3 hipPosition = upperLeg.position;
            Vector3 footTarget = foot.position;
            float upperLength = Vector3.Distance(upperLeg.position, lowerLeg.position);
            float lowerLength = Vector3.Distance(lowerLeg.position, foot.position);
            Vector3 toFoot = footTarget - hipPosition;
            float rawDistance = toFoot.magnitude;
            if (upperLength < 0.001f || lowerLength < 0.001f || rawDistance < 0.001f)
            {
                return;
            }

            Vector3 direction = toFoot / rawDistance;
            float minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
            float maximumReach = upperLength + lowerLength - 0.001f;
            float distance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);

            Transform motor = character.MotorBody.transform;
            Vector3 poleDirection =
                motor.forward * kneeForwardBias +
                motor.right * (sideSign * kneeOutwardBias);
            Vector3 bendDirection = Vector3.ProjectOnPlane(poleDirection, direction);
            if (bendDirection.sqrMagnitude < 0.0001f)
            {
                bendDirection = Vector3.ProjectOnPlane(motor.forward, direction);
            }
            if (bendDirection.sqrMagnitude < 0.0001f)
            {
                bendDirection = Vector3.Cross(direction, motor.right * sideSign);
            }
            bendDirection.Normalize();

            float along = (upperLength * upperLength + distance * distance -
                           lowerLength * lowerLength) / (2f * distance);
            float heightSquared = Mathf.Max(0f, upperLength * upperLength - along * along);
            Vector3 desiredKnee = hipPosition + direction * along +
                                  bendDirection * Mathf.Sqrt(heightSquared);

            Vector3 currentUpperDirection = lowerLeg.position - upperLeg.position;
            Vector3 desiredUpperDirection = desiredKnee - upperLeg.position;
            if (currentUpperDirection.sqrMagnitude > 0.0001f &&
                desiredUpperDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion correction = Quaternion.FromToRotation(
                    currentUpperDirection,
                    desiredUpperDirection);
                upperLeg.rotation = Quaternion.Slerp(
                    upperLeg.rotation,
                    correction * upperLeg.rotation,
                    kneeCorrectionStrength);
            }

            Vector3 currentLowerDirection = foot.position - lowerLeg.position;
            Vector3 desiredLowerDirection = footTarget - lowerLeg.position;
            if (currentLowerDirection.sqrMagnitude > 0.0001f &&
                desiredLowerDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion correction = Quaternion.FromToRotation(
                    currentLowerDirection,
                    desiredLowerDirection);
                lowerLeg.rotation = Quaternion.Slerp(
                    lowerLeg.rotation,
                    correction * lowerLeg.rotation,
                    kneeCorrectionStrength);
            }
        }

        private void ResetPose()
        {
            smoothedSpeed = 0f;
            if (poseHandler == null)
            {
                return;
            }

            poseHandler.GetHumanPose(ref pose);
            SetMuscle(leftUpperLegMuscle, 0f);
            SetMuscle(rightUpperLegMuscle, 0f);
            SetMuscle(leftLowerLegMuscle, 0f);
            SetMuscle(rightLowerLegMuscle, 0f);
            SetMuscle(leftFootMuscle, 0f);
            SetMuscle(rightFootMuscle, 0f);
            poseHandler.SetHumanPose(ref pose);
        }

        private void DisposePoseHandler()
        {
            if (poseHandler != null)
            {
                poseHandler.Dispose();
                poseHandler = null;
            }
        }

        private void SetMuscle(int index, float value)
        {
            if (index < 0 || pose.muscles == null || index >= pose.muscles.Length)
            {
                return;
            }

            pose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
        }

        private void ApplyVersionedDefaults()
        {
            if (settingsVersion >= CurrentSettingsVersion)
            {
                return;
            }

            legSwing = 0.3f;
            kneeBend = 0.5f;
            footLift = 0.12f;
            armSwing = 0.28f;
            armOutward = 0.07f;
            armDownWeight = 1f;
            kneeForwardBias = 0.92f;
            kneeOutwardBias = 0.18f;
            kneeCorrectionStrength = 1f;
            speedBlendRate = 7f;
            settingsVersion = CurrentSettingsVersion;
        }

        private static int FindMuscle(params string[] candidates)
        {
            string[] muscleNames = HumanTrait.MuscleName;
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                for (int i = 0; i < muscleNames.Length; i++)
                {
                    if (string.Equals(
                            muscleNames[i],
                            candidates[candidateIndex],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyVersionedDefaults();
            referenceSpeed = Mathf.Max(0.1f, referenceSpeed);
            stepFrequency = Mathf.Max(0f, stepFrequency);
            legSwing = Mathf.Clamp01(legSwing);
            kneeBend = Mathf.Clamp01(kneeBend);
            footLift = Mathf.Clamp01(footLift);
            speedBlendRate = Mathf.Max(0f, speedBlendRate);
            armSwing = Mathf.Clamp01(armSwing);
            armOutward = Mathf.Clamp(armOutward, 0f, 0.35f);
            armDownWeight = Mathf.Clamp(armDownWeight, 0.6f, 1.4f);
            kneeForwardBias = Mathf.Clamp01(kneeForwardBias);
            kneeOutwardBias = Mathf.Clamp(kneeOutwardBias, 0f, 0.75f);
            kneeCorrectionStrength = Mathf.Clamp01(kneeCorrectionStrength);
        }
#endif
    }
}

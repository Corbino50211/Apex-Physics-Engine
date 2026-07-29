using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Supplies a simple Humanoid walking pose when no Animator Controller is assigned.
    /// It modifies the hidden animated target, so the visible PC physical character still
    /// follows the same stable target-driven pipeline.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class ApexPCProceduralGait : MonoBehaviour
    {
        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private bool proceduralWhenNoController = true;
        [SerializeField] private bool forceProceduralGait;
        [SerializeField, Min(0.1f)] private float referenceSpeed = 3.5f;
        [SerializeField, Min(0f)] private float stepFrequency = 1.8f;
        [SerializeField, Range(0f, 1f)] private float legSwing = 0.55f;
        [SerializeField, Range(0f, 1f)] private float kneeBend = 0.7f;
        [SerializeField, Range(0f, 1f)] private float armSwing = 0.35f;
        [SerializeField, Range(0f, 1f)] private float footLift = 0.22f;

        private HumanPoseHandler poseHandler;
        private HumanPose pose;
        private float gaitPhase;

        private int leftUpperLeg = -1;
        private int rightUpperLeg = -1;
        private int leftLowerLeg = -1;
        private int rightLowerLeg = -1;
        private int leftFoot = -1;
        private int rightFoot = -1;
        private int leftArm = -1;
        private int rightArm = -1;

        public bool IsUsingProceduralGait => ShouldUseProceduralGait();

        private void Awake()
        {
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
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
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.1f, referenceSpeed));

            gaitPhase += Time.deltaTime * Mathf.Lerp(0.4f, stepFrequency, normalizedSpeed) *
                         Mathf.PI * 2f;

            poseHandler.GetHumanPose(ref pose);
            float wave = Mathf.Sin(gaitPhase);
            float leftLiftAmount = Mathf.Max(0f, -wave);
            float rightLiftAmount = Mathf.Max(0f, wave);

            SetMuscle(leftUpperLeg, wave * legSwing * normalizedSpeed);
            SetMuscle(rightUpperLeg, -wave * legSwing * normalizedSpeed);
            SetMuscle(leftLowerLeg, -leftLiftAmount * kneeBend * normalizedSpeed);
            SetMuscle(rightLowerLeg, -rightLiftAmount * kneeBend * normalizedSpeed);
            SetMuscle(leftFoot, leftLiftAmount * footLift * normalizedSpeed);
            SetMuscle(rightFoot, rightLiftAmount * footLift * normalizedSpeed);
            SetMuscle(leftArm, -wave * armSwing * normalizedSpeed);
            SetMuscle(rightArm, wave * armSwing * normalizedSpeed);

            poseHandler.SetHumanPose(ref pose);
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            character = owner;
            targetAnimator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetAnimator
                : null;
            DisposePoseHandler();
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

            leftUpperLeg = FindMuscle("Left Upper Leg Front-Back");
            rightUpperLeg = FindMuscle("Right Upper Leg Front-Back");
            leftLowerLeg = FindMuscle("Left Lower Leg Stretch");
            rightLowerLeg = FindMuscle("Right Lower Leg Stretch");
            leftFoot = FindMuscle("Left Foot Up-Down");
            rightFoot = FindMuscle("Right Foot Up-Down");
            leftArm = FindMuscle("Left Arm Front-Back", "Left Upper Arm Front-Back");
            rightArm = FindMuscle("Right Arm Front-Back", "Right Upper Arm Front-Back");
        }

        private void ResetPose()
        {
            if (poseHandler == null)
            {
                return;
            }

            poseHandler.GetHumanPose(ref pose);
            SetMuscle(leftUpperLeg, 0f);
            SetMuscle(rightUpperLeg, 0f);
            SetMuscle(leftLowerLeg, 0f);
            SetMuscle(rightLowerLeg, 0f);
            SetMuscle(leftFoot, 0f);
            SetMuscle(rightFoot, 0f);
            SetMuscle(leftArm, 0f);
            SetMuscle(rightArm, 0f);
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
            referenceSpeed = Mathf.Max(0.1f, referenceSpeed);
            stepFrequency = Mathf.Max(0f, stepFrequency);
            legSwing = Mathf.Clamp01(legSwing);
            kneeBend = Mathf.Clamp01(kneeBend);
            armSwing = Mathf.Clamp01(armSwing);
            footLift = Mathf.Clamp01(footLift);
        }
#endif
    }
}

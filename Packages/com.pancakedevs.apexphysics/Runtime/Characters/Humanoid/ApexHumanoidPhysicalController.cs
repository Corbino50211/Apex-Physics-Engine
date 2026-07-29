using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    public enum ApexHumanoidPhysicalState
    {
        Active = 0,
        Ragdoll = 1,
        GettingUp = 2
    }

    /// <summary>
    /// Coordinates locomotion, animation, ragdoll release, and get-up recovery for a
    /// converted physical humanoid. The support body is authoritative while Active,
    /// the articulated body is fully released while Ragdoll, and the support body is
    /// repositioned beneath the character while GettingUp.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidPhysicalController : MonoBehaviour
    {
        [Header("Humanoid Systems")]
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private ApexHumanoidSupportRig supportRig;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private Animator targetAnimator;

        [Header("Animator Parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string movingParameter = "Moving";
        [SerializeField] private string groundedParameter = "Grounded";
        [SerializeField] private string ragdollParameter = "Ragdoll";
        [SerializeField] private string getUpFrontTrigger = "GetUpFront";
        [SerializeField] private string getUpBackTrigger = "GetUpBack";
        [SerializeField, Min(0.01f)] private float animationReferenceSpeed = 3.5f;

        [Header("Procedural Locomotion Fallback")]
        [SerializeField] private bool proceduralGaitWhenNoController = true;
        [SerializeField] private bool forceProceduralGait;
        [SerializeField, Min(0f)] private float stepFrequency = 1.8f;
        [SerializeField, Range(0f, 1f)] private float legSwing = 0.55f;
        [SerializeField, Range(0f, 1f)] private float kneeBend = 0.72f;
        [SerializeField, Range(0f, 1f)] private float armSwing = 0.35f;
        [SerializeField, Range(0f, 1f)] private float footLift = 0.25f;

        [Header("Recovery")]
        [SerializeField] private bool automaticallyGetUp = true;
        [SerializeField, Min(0.1f)] private float fallbackGetUpDuration = 1.5f;

        private readonly Dictionary<int, AnimatorControllerParameterType> animatorParameters =
            new Dictionary<int, AnimatorControllerParameterType>();

        private ApexHumanoidPhysicalState currentState = ApexHumanoidPhysicalState.Active;
        private float stateStartedAt;
        private bool subscribed;
        private bool configured;

        private HumanPoseHandler humanPoseHandler;
        private HumanPose humanPose;
        private float gaitPhase;

        private int leftUpperLeg = -1;
        private int rightUpperLeg = -1;
        private int leftLowerLeg = -1;
        private int rightLowerLeg = -1;
        private int leftFoot = -1;
        private int rightFoot = -1;
        private int leftArm = -1;
        private int rightArm = -1;

        public ApexHumanoidPhysicalState State => currentState;
        public ApexPhysicalHumanoid Humanoid => humanoid;
        public ApexHumanoidSupportRig SupportRig => supportRig;
        public bool UsesProceduralGait => ShouldUseProceduralGait();

        private void Awake()
        {
            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (humanPoseHandler != null)
            {
                humanPoseHandler.Dispose();
                humanPoseHandler = null;
            }
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            UpdateRecoveryState();
            UpdateAnimation();
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            Unsubscribe();

            humanoid = owner;
            supportRig = owner != null
                ? (owner.SupportRig != null ? owner.SupportRig : owner.GetComponent<ApexHumanoidSupportRig>())
                : GetComponent<ApexHumanoidSupportRig>();
            activeRagdoll = owner != null ? owner.ActiveRagdoll : null;
            targetAnimator = owner != null ? owner.TargetAnimator : null;

            if (supportRig != null && owner != null)
            {
                supportRig.Configure(owner);
            }

            DisableLegacySupportAddons();
            CacheAnimatorParameters();

            if (Application.isPlaying)
            {
                InitializeProceduralPose();
            }

            configured = humanoid != null && supportRig != null && activeRagdoll != null;
            currentState = ConvertState(activeRagdoll != null
                ? activeRagdoll.State
                : ApexRagdollState.Active);
            stateStartedAt = Time.time;

            Subscribe();
            ApplyCurrentStateImmediately();
        }

        public void KnockDown()
        {
            activeRagdoll?.KnockDown();
        }

        public void BeginGetUp()
        {
            if (activeRagdoll == null || activeRagdoll.State == ApexRagdollState.Active)
            {
                return;
            }

            activeRagdoll.BeginRecovery();
        }

        public void RecoverImmediately()
        {
            if (activeRagdoll == null)
            {
                return;
            }

            supportRig?.FinishRecovery();
            activeRagdoll.RecoverImmediately();
        }

        private void Subscribe()
        {
            if (subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged += HandleRagdollStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged -= HandleRagdollStateChanged;
            subscribed = false;
        }

        private void HandleRagdollStateChanged(ApexRagdollState state)
        {
            stateStartedAt = Time.time;

            switch (state)
            {
                case ApexRagdollState.Limp:
                    EnterRagdoll();
                    break;
                case ApexRagdollState.Recovering:
                    EnterGettingUp();
                    break;
                default:
                    EnterActive();
                    break;
            }
        }

        private void ApplyCurrentStateImmediately()
        {
            switch (currentState)
            {
                case ApexHumanoidPhysicalState.Ragdoll:
                    supportRig?.ReleaseForRagdoll();
                    break;
                case ApexHumanoidPhysicalState.GettingUp:
                    supportRig?.BeginRecovery(GetRecoveryDuration());
                    break;
                default:
                    supportRig?.FinishRecovery();
                    break;
            }

            SetAnimatorBool(ragdollParameter, currentState == ApexHumanoidPhysicalState.Ragdoll);
        }

        private void EnterRagdoll()
        {
            currentState = ApexHumanoidPhysicalState.Ragdoll;
            supportRig?.ReleaseForRagdoll();
            SetAnimatorBool(ragdollParameter, true);
            ResetProceduralPose();
        }

        private void EnterGettingUp()
        {
            currentState = ApexHumanoidPhysicalState.GettingUp;
            supportRig?.BeginRecovery(GetRecoveryDuration());
            SetAnimatorBool(ragdollParameter, false);

            bool faceUp = IsPhysicalBodyFaceUp();
            SetAnimatorTrigger(faceUp ? getUpBackTrigger : getUpFrontTrigger);
            ResetProceduralPose();
        }

        private void EnterActive()
        {
            currentState = ApexHumanoidPhysicalState.Active;
            supportRig?.FinishRecovery();
            SetAnimatorBool(ragdollParameter, false);
        }

        private void UpdateRecoveryState()
        {
            if (!automaticallyGetUp || activeRagdoll == null || activeRagdoll.Profile == null)
            {
                return;
            }

            if (currentState == ApexHumanoidPhysicalState.Ragdoll &&
                Time.time - stateStartedAt >= activeRagdoll.Profile.LimpDuration)
            {
                activeRagdoll.BeginRecovery();
            }
        }

        private void UpdateAnimation()
        {
            if (targetAnimator == null || supportRig == null || supportRig.SupportBody == null)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                supportRig.SupportBody.velocity,
                Vector3.up);
            float speed = horizontalVelocity.magnitude;
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.01f, animationReferenceSpeed));
            bool moving = currentState == ApexHumanoidPhysicalState.Active && normalizedSpeed > 0.03f;

            SetAnimatorFloat(speedParameter, normalizedSpeed);
            SetAnimatorBool(movingParameter, moving);
            SetAnimatorBool(groundedParameter, supportRig.IsGrounded);

            if (!ShouldUseProceduralGait())
            {
                return;
            }

            if (currentState == ApexHumanoidPhysicalState.Active)
            {
                ApplyProceduralGait(normalizedSpeed);
            }
            else if (currentState == ApexHumanoidPhysicalState.GettingUp)
            {
                ResetProceduralPose();
            }
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

            if (!proceduralGaitWhenNoController)
            {
                return false;
            }

            int speedHash = Animator.StringToHash(speedParameter ?? string.Empty);
            return targetAnimator.runtimeAnimatorController == null ||
                   !HasAnimatorParameter(speedHash, AnimatorControllerParameterType.Float);
        }

        private void InitializeProceduralPose()
        {
            if (targetAnimator == null || targetAnimator.avatar == null ||
                !targetAnimator.avatar.isValid || !targetAnimator.isHuman)
            {
                return;
            }

            if (humanPoseHandler != null)
            {
                humanPoseHandler.Dispose();
            }

            humanPoseHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
            humanPose = new HumanPose
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

        private void ApplyProceduralGait(float normalizedSpeed)
        {
            if (humanPoseHandler == null)
            {
                InitializeProceduralPose();
            }

            if (humanPoseHandler == null)
            {
                return;
            }

            gaitPhase += Time.deltaTime * Mathf.Lerp(0.5f, stepFrequency, normalizedSpeed) *
                         Mathf.PI * 2f;

            humanPoseHandler.GetHumanPose(ref humanPose);
            float wave = Mathf.Sin(gaitPhase);
            float leftLift = Mathf.Max(0f, -wave);
            float rightLift = Mathf.Max(0f, wave);

            SetMuscle(leftUpperLeg, wave * legSwing * normalizedSpeed);
            SetMuscle(rightUpperLeg, -wave * legSwing * normalizedSpeed);
            SetMuscle(leftLowerLeg, -leftLift * kneeBend * normalizedSpeed);
            SetMuscle(rightLowerLeg, -rightLift * kneeBend * normalizedSpeed);
            SetMuscle(leftFoot, leftLift * footLift * normalizedSpeed);
            SetMuscle(rightFoot, rightLift * footLift * normalizedSpeed);
            SetMuscle(leftArm, -wave * armSwing * normalizedSpeed);
            SetMuscle(rightArm, wave * armSwing * normalizedSpeed);

            humanPoseHandler.SetHumanPose(ref humanPose);
        }

        private void ResetProceduralPose()
        {
            if (humanPoseHandler == null)
            {
                return;
            }

            humanPoseHandler.GetHumanPose(ref humanPose);
            SetMuscle(leftUpperLeg, 0f);
            SetMuscle(rightUpperLeg, 0f);
            SetMuscle(leftLowerLeg, 0f);
            SetMuscle(rightLowerLeg, 0f);
            SetMuscle(leftFoot, 0f);
            SetMuscle(rightFoot, 0f);
            SetMuscle(leftArm, 0f);
            SetMuscle(rightArm, 0f);
            humanPoseHandler.SetHumanPose(ref humanPose);
        }

        private void SetMuscle(int index, float value)
        {
            if (index < 0 || humanPose.muscles == null || index >= humanPose.muscles.Length)
            {
                return;
            }

            humanPose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
        }

        private static int FindMuscle(params string[] candidates)
        {
            string[] muscleNames = HumanTrait.MuscleName;
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                string candidate = candidates[candidateIndex];
                for (int i = 0; i < muscleNames.Length; i++)
                {
                    if (string.Equals(muscleNames[i], candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private bool IsPhysicalBodyFaceUp()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return true;
            }

            Animator physicalAnimator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (physicalAnimator == null || !physicalAnimator.isHuman)
            {
                return true;
            }

            Transform chest = physicalAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
            {
                chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }
            if (chest == null)
            {
                chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Spine);
            }

            return chest == null || Vector3.Dot(chest.forward, Vector3.up) >= 0f;
        }

        private float GetRecoveryDuration()
        {
            if (activeRagdoll != null && activeRagdoll.Profile != null)
            {
                return Mathf.Max(0.1f, activeRagdoll.Profile.RecoveryBlendDuration);
            }

            return fallbackGetUpDuration;
        }

        private void DisableLegacySupportAddons()
        {
            ApexHumanoidLocoballRig locoballRig = GetComponent<ApexHumanoidLocoballRig>();
            if (locoballRig != null)
            {
                locoballRig.enabled = false;
            }

            ApexHumanoidTorsoHarness torsoHarness = GetComponent<ApexHumanoidTorsoHarness>();
            if (torsoHarness != null)
            {
                torsoHarness.enabled = false;
            }

            ApexHumanoidFootTether[] tethers = GetComponentsInChildren<ApexHumanoidFootTether>(true);
            for (int i = 0; i < tethers.Length; i++)
            {
                tethers[i]?.SetTetherActive(false);
            }

            if (supportRig != null && supportRig.SupportBody != null)
            {
                Transform legacyLocoball = supportRig.SupportBody.transform.Find("Apex Humanoid Locoball");
                if (legacyLocoball != null)
                {
                    Collider[] colliders = legacyLocoball.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        colliders[i].enabled = false;
                    }
                }
            }
        }

        private void CacheAnimatorParameters()
        {
            animatorParameters.Clear();
            if (targetAnimator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = targetAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                animatorParameters[parameters[i].nameHash] = parameters[i].type;
            }
        }

        private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
        {
            return hash != 0 && animatorParameters.TryGetValue(hash, out AnimatorControllerParameterType found) &&
                   found == type;
        }

        private void SetAnimatorFloat(string parameter, float value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            int hash = Animator.StringToHash(parameter);
            if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Float))
            {
                targetAnimator.SetFloat(hash, value, 0.12f, Time.deltaTime);
            }
        }

        private void SetAnimatorBool(string parameter, bool value)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            int hash = Animator.StringToHash(parameter);
            if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Bool))
            {
                targetAnimator.SetBool(hash, value);
            }
        }

        private void SetAnimatorTrigger(string parameter)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            int hash = Animator.StringToHash(parameter);
            if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Trigger))
            {
                targetAnimator.ResetTrigger(hash);
                targetAnimator.SetTrigger(hash);
            }
        }

        private static ApexHumanoidPhysicalState ConvertState(ApexRagdollState state)
        {
            switch (state)
            {
                case ApexRagdollState.Limp:
                    return ApexHumanoidPhysicalState.Ragdoll;
                case ApexRagdollState.Recovering:
                    return ApexHumanoidPhysicalState.GettingUp;
                default:
                    return ApexHumanoidPhysicalState.Active;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            animationReferenceSpeed = Mathf.Max(0.01f, animationReferenceSpeed);
            stepFrequency = Mathf.Max(0f, stepFrequency);
            legSwing = Mathf.Clamp01(legSwing);
            kneeBend = Mathf.Clamp01(kneeBend);
            armSwing = Mathf.Clamp01(armSwing);
            footLift = Mathf.Clamp01(footLift);
            fallbackGetUpDuration = Mathf.Max(0.1f, fallbackGetUpDuration);
        }
#endif
    }
}

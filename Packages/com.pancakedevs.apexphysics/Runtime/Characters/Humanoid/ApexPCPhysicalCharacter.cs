using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    public enum ApexPCCharacterState
    {
        Active = 0,
        Staggered = 1,
        Ragdoll = 2,
        GettingUp = 3
    }

    /// <summary>
    /// Stable PC-first physical character controller. While Active, a single capsule
    /// Rigidbody handles locomotion and the visible physical skeleton follows the hidden
    /// animated target exactly. Hard external impacts release the skeleton into a full
    /// ragdoll. Recovery blends the fallen body back to the animated target before
    /// restoring normal locomotion.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class ApexPCPhysicalCharacter : MonoBehaviour
    {
        [Header("Generated Character")]
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform targetHips;
        [SerializeField] private ApexNPCNavigator navigator;
        [SerializeField] private ApexNPCBrain brain;

        [Header("PC Character Motor")]
        [SerializeField] private Rigidbody motorBody;
        [SerializeField] private CapsuleCollider motorCollider;
        [SerializeField] private Transform hipsAnchor;
        [SerializeField, Min(0.1f)] private float movementSpeed = 3.5f;
        [SerializeField, Min(0f)] private float acceleration = 28f;
        [SerializeField, Min(0f)] private float braking = 34f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [SerializeField, Min(1f)] private float motorMass = 70f;

        [Header("Impact Response")]
        [SerializeField, Min(0f)] private float minimumRagdollImpactSpeed = 4.5f;
        [SerializeField, Min(0f)] private float minimumRagdollImpulse = 3f;
        [SerializeField, Min(0f)] private float startupImpactDelay = 0.75f;
        [SerializeField, Min(0f)] private float impactImpulseMultiplier = 0.45f;

        [Header("Recovery")]
        [SerializeField] private bool automaticallyGetUp = true;
        [SerializeField, Min(0.1f)] private float ragdollDuration = 3f;
        [SerializeField, Min(0.1f)] private float getUpDuration = 1.4f;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string movingParameter = "Moving";
        [SerializeField] private string groundedParameter = "Grounded";
        [SerializeField] private string ragdollParameter = "Ragdoll";
        [SerializeField] private string getUpFrontTrigger = "GetUpFront";
        [SerializeField] private string getUpBackTrigger = "GetUpBack";

        [Header("Runtime State")]
        [SerializeField] private ApexPCCharacterState currentState = ApexPCCharacterState.Active;
        [SerializeField] private bool grounded;

        private readonly List<ApexRagdollBone> bones = new List<ApexRagdollBone>();
        private readonly List<Rigidbody> boneBodies = new List<Rigidbody>();
        private readonly List<Vector3> recoveryStartPositions = new List<Vector3>();
        private readonly List<Quaternion> recoveryStartRotations = new List<Quaternion>();
        private readonly Dictionary<int, AnimatorControllerParameterType> animatorParameters =
            new Dictionary<int, AnimatorControllerParameterType>();
        private readonly RaycastHit[] groundHits = new RaycastHit[24];

        private ApexBody motorApexBody;
        private float stateStartedAt;
        private float impactsArmedAt;
        private float characterHeight = 1.8f;
        private float standingHipsHeight = 0.95f;
        private bool configured;
        private bool subscribed;

        public ApexPCCharacterState State => currentState;
        public ApexPhysicalHumanoid Humanoid => humanoid;
        public Rigidbody MotorBody => motorBody;
        public CapsuleCollider MotorCollider => motorCollider;
        public bool Grounded => grounded;
        public bool IsReady => configured && motorBody != null && bones.Count > 0;

        public event Action<ApexPCCharacterState> StateChanged;

        private void Awake()
        {
            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
        }

        private void OnEnable()
        {
            SubscribeToImpacts();
        }

        private void OnDisable()
        {
            UnsubscribeFromImpacts();
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            if (currentState == ApexPCCharacterState.Ragdoll && automaticallyGetUp &&
                Time.time - stateStartedAt >= ragdollDuration)
            {
                BeginGetUp();
            }

            if (currentState == ApexPCCharacterState.GettingUp &&
                Time.time - stateStartedAt >= getUpDuration)
            {
                Activate();
            }

            UpdateAnimatorParameters();
        }

        private void FixedUpdate()
        {
            if (!configured || motorBody == null)
            {
                return;
            }

            grounded = CheckGrounded();

            if (currentState != ApexPCCharacterState.Active &&
                currentState != ApexPCCharacterState.Staggered)
            {
                return;
            }

            ApplyMotorMovement(currentState == ApexPCCharacterState.Staggered ? 0.25f : 1f);
        }

        private void LateUpdate()
        {
            if (!configured)
            {
                return;
            }

            if (currentState == ApexPCCharacterState.Active ||
                currentState == ApexPCCharacterState.Staggered)
            {
                CopyAnimatedPoseToPhysical(1f);
            }
            else if (currentState == ApexPCCharacterState.GettingUp)
            {
                float progress = Mathf.Clamp01((Time.time - stateStartedAt) / getUpDuration);
                progress = progress * progress * (3f - 2f * progress);
                BlendPhysicalBodyToAnimatedPose(progress);
            }
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            UnsubscribeFromImpacts();

            humanoid = owner;
            if (humanoid == null || humanoid.Mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                configured = false;
                return;
            }

            targetAnimator = humanoid.TargetAnimator;
            targetHips = humanoid.TargetHips;
            navigator = humanoid.NPCNavigator;
            brain = humanoid.NPCBrain;

            DisableLegacyCharacterControllers();
            CachePhysicalBones();
            BuildOrRepairMotor();
            ConfigureTargetRoot();
            ConfigureCollisionFiltering();
            CacheAnimatorParameters();

            configured = motorBody != null && motorCollider != null && hipsAnchor != null &&
                         targetAnimator != null && targetHips != null && bones.Count > 0;

            SubscribeToImpacts();
            if (configured)
            {
                Activate();
            }
        }

        public void Rebuild()
        {
            configured = false;
            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
        }

        public void SetDestination(Vector3 worldPosition)
        {
            navigator?.SetDestination(worldPosition);
        }

        public void SetTarget(Transform target)
        {
            navigator?.SetTarget(target);
        }

        public void SetIdle()
        {
            brain?.SetMode(ApexNPCBehaviorMode.Idle);
        }

        public void SetWander()
        {
            brain?.SetMode(ApexNPCBehaviorMode.Wander);
        }

        public void Stagger(float duration = 0.45f)
        {
            if (currentState != ApexPCCharacterState.Active)
            {
                return;
            }

            SetState(ApexPCCharacterState.Staggered);
            CancelInvoke(nameof(EndStagger));
            Invoke(nameof(EndStagger), Mathf.Max(0.05f, duration));
        }

        public void KnockDown()
        {
            KnockDown(null, null);
        }

        public void BeginGetUp()
        {
            if (!configured || currentState != ApexPCCharacterState.Ragdoll)
            {
                return;
            }

            PositionMotorUnderRagdoll();
            CacheRecoveryStartPose();
            SetBoneSimulation(false);

            if (motorBody != null)
            {
                motorBody.isKinematic = true;
                motorBody.useGravity = false;
                motorBody.velocity = Vector3.zero;
                motorBody.angularVelocity = Vector3.zero;
            }

            if (motorCollider != null)
            {
                motorCollider.enabled = false;
            }

            ConfigureTargetRoot();
            SetAnimatorBool(ragdollParameter, false);
            SetAnimatorTrigger(IsBodyFaceUp() ? getUpBackTrigger : getUpFrontTrigger);
            SetState(ApexPCCharacterState.GettingUp);
        }

        public void Activate()
        {
            if (!configured)
            {
                return;
            }

            SetBoneSimulation(false);

            if (motorBody != null)
            {
                motorBody.isKinematic = false;
                motorBody.useGravity = true;
                motorBody.constraints = RigidbodyConstraints.FreezeRotationX |
                                        RigidbodyConstraints.FreezeRotationZ;
                motorBody.velocity = Vector3.zero;
                motorBody.angularVelocity = Vector3.zero;
            }

            if (motorCollider != null)
            {
                motorCollider.enabled = true;
            }

            if (humanoid.TargetRootDriver != null)
            {
                humanoid.TargetRootDriver.enabled = true;
                humanoid.TargetRootDriver.SnapNow();
            }

            SetAnimatorBool(ragdollParameter, false);
            CopyAnimatedPoseToPhysical(1f);
            impactsArmedAt = Time.time + startupImpactDelay;
            SetState(ApexPCCharacterState.Active);
        }

        private void EndStagger()
        {
            if (currentState == ApexPCCharacterState.Staggered)
            {
                SetState(ApexPCCharacterState.Active);
            }
        }

        private void KnockDown(ApexRagdollBone impactedBone, Collision collision)
        {
            if (!configured || currentState == ApexPCCharacterState.Ragdoll ||
                currentState == ApexPCCharacterState.GettingUp)
            {
                return;
            }

            CancelInvoke(nameof(EndStagger));

            Vector3 inheritedVelocity = motorBody != null ? motorBody.velocity : Vector3.zero;
            if (motorCollider != null)
            {
                motorCollider.enabled = false;
            }

            if (motorBody != null)
            {
                motorBody.velocity = Vector3.zero;
                motorBody.angularVelocity = Vector3.zero;
                motorBody.useGravity = false;
                motorBody.isKinematic = true;
            }

            if (humanoid.TargetRootDriver != null)
            {
                humanoid.TargetRootDriver.enabled = false;
            }

            SetBoneSimulation(true, inheritedVelocity);
            SetAnimatorBool(ragdollParameter, true);
            SetState(ApexPCCharacterState.Ragdoll);

            if (impactedBone != null && impactedBone.Body != null && collision != null)
            {
                Vector3 impulse = collision.relativeVelocity *
                                  (impactedBone.Body.mass * impactImpulseMultiplier);
                impactedBone.Body.AddForce(impulse, ForceMode.Impulse);
            }
        }

        private void SetState(ApexPCCharacterState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;
            stateStartedAt = Time.time;
            StateChanged?.Invoke(currentState);
        }

        private void DisableLegacyCharacterControllers()
        {
            if (humanoid.ActiveRagdoll != null)
            {
                humanoid.ActiveRagdoll.enabled = false;
            }

            if (humanoid.SupportRig != null)
            {
                humanoid.SupportRig.enabled = false;
                Rigidbody oldSupport = humanoid.SupportRig.SupportBody;
                if (oldSupport != null)
                {
                    oldSupport.isKinematic = true;
                    oldSupport.useGravity = false;
                    Collider[] oldColliders = oldSupport.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < oldColliders.Length; i++)
                    {
                        oldColliders[i].enabled = false;
                    }
                }
            }

            if (humanoid.PhysicalController != null)
            {
                humanoid.PhysicalController.enabled = false;
            }

            if (humanoid.LocoballRig != null)
            {
                humanoid.LocoballRig.enabled = false;
            }

            if (humanoid.TorsoHarness != null)
            {
                humanoid.TorsoHarness.enabled = false;
            }

            if (humanoid.NPCMotor != null)
            {
                humanoid.NPCMotor.enabled = false;
            }

            if (humanoid.HumanoidPlayerMotor != null)
            {
                humanoid.HumanoidPlayerMotor.enabled = false;
            }

            ApexHumanoidFootTether[] tethers = GetComponentsInChildren<ApexHumanoidFootTether>(true);
            for (int i = 0; i < tethers.Length; i++)
            {
                tethers[i]?.SetTetherActive(false);
            }

            ConfigurableJoint rootJoint = humanoid.PhysicalHips != null
                ? humanoid.PhysicalHips.Joint
                : null;
            if (rootJoint != null)
            {
                rootJoint.connectedBody = null;
                rootJoint.xMotion = ConfigurableJointMotion.Free;
                rootJoint.yMotion = ConfigurableJointMotion.Free;
                rootJoint.zMotion = ConfigurableJointMotion.Free;
                rootJoint.angularXMotion = ConfigurableJointMotion.Free;
                rootJoint.angularYMotion = ConfigurableJointMotion.Free;
                rootJoint.angularZMotion = ConfigurableJointMotion.Free;
            }
        }

        private void CachePhysicalBones()
        {
            bones.Clear();
            boneBodies.Clear();

            if (humanoid.PhysicalCharacter == null)
            {
                return;
            }

            ApexRagdollBone[] found =
                humanoid.PhysicalCharacter.GetComponentsInChildren<ApexRagdollBone>(true);
            for (int i = 0; i < found.Length; i++)
            {
                ApexRagdollBone bone = found[i];
                if (bone == null || bone.Target == null || bone.Body == null)
                {
                    continue;
                }

                bones.Add(bone);
                boneBodies.Add(bone.Body);
            }
        }

        private void BuildOrRepairMotor()
        {
            if (humanoid.PhysicalHips == null)
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            characterHeight = CalculateCharacterHeight();
            float groundHeight = ResolveGroundHeight(hips.position);
            standingHipsHeight = Mathf.Max(0.25f, hips.position.y - groundHeight);

            Transform motorTransform = transform.Find("Apex PC Character Motor");
            if (motorTransform == null)
            {
                GameObject motorObject = new GameObject("Apex PC Character Motor");
                motorTransform = motorObject.transform;
                motorTransform.SetParent(transform, true);
            }

            Vector3 forward = Vector3.ProjectOnPlane(hips.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            motorTransform.SetPositionAndRotation(
                new Vector3(hips.position.x, groundHeight, hips.position.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            motorTransform.localScale = Vector3.one;

            motorBody = GetOrAdd<Rigidbody>(motorTransform.gameObject);
            motorBody.mass = motorMass;
            motorBody.useGravity = true;
            motorBody.isKinematic = false;
            motorBody.interpolation = RigidbodyInterpolation.Interpolate;
            motorBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            motorBody.constraints = RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationZ;
            motorBody.solverIterations = 12;
            motorBody.solverVelocityIterations = 6;

            motorCollider = GetOrAdd<CapsuleCollider>(motorTransform.gameObject);
            motorCollider.direction = 1;
            motorCollider.radius = Mathf.Clamp(characterHeight * 0.15f, 0.12f, 0.45f);
            motorCollider.height = Mathf.Max(
                motorCollider.radius * 2f,
                characterHeight * 0.92f);
            motorCollider.center = Vector3.up * (motorCollider.height * 0.5f);

            motorApexBody = GetOrAdd<ApexBody>(motorTransform.gameObject);

            hipsAnchor = motorTransform.Find("Hips Anchor");
            if (hipsAnchor == null)
            {
                GameObject anchorObject = new GameObject("Hips Anchor");
                hipsAnchor = anchorObject.transform;
                hipsAnchor.SetParent(motorTransform, false);
            }

            hipsAnchor.position = targetHips != null ? targetHips.position : hips.position;
            hipsAnchor.rotation = targetHips != null ? targetHips.rotation : hips.rotation;
            hipsAnchor.localScale = Vector3.one;
        }

        private void ConfigureTargetRoot()
        {
            if (humanoid.TargetRootDriver == null || hipsAnchor == null || targetHips == null)
            {
                return;
            }

            humanoid.TargetRootDriver.enabled = true;
            humanoid.TargetRootDriver.Configure(
                hipsAnchor,
                targetHips,
                true,
                true,
                false);
            humanoid.TargetRootDriver.SnapNow();
        }

        private void ConfigureCollisionFiltering()
        {
            if (motorCollider == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            Collider[] physicalColliders =
                humanoid.PhysicalCharacter.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < physicalColliders.Length; i++)
            {
                Collider physicalCollider = physicalColliders[i];
                if (physicalCollider != null && physicalCollider != motorCollider)
                {
                    Physics.IgnoreCollision(motorCollider, physicalCollider, true);
                }
            }
        }

        private void SetBoneSimulation(bool dynamic, Vector3 inheritedVelocity = default)
        {
            for (int i = 0; i < boneBodies.Count; i++)
            {
                Rigidbody body = boneBodies[i];
                if (body == null)
                {
                    continue;
                }

                body.velocity = dynamic ? inheritedVelocity : Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = dynamic;
                body.isKinematic = !dynamic;
                if (dynamic)
                {
                    body.WakeUp();
                }
            }
        }

        private void CopyAnimatedPoseToPhysical(float blend)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                ApexRagdollBone bone = bones[i];
                Rigidbody body = boneBodies[i];
                if (bone == null || bone.Target == null || body == null)
                {
                    continue;
                }

                Vector3 position = blend >= 0.999f
                    ? bone.Target.position
                    : Vector3.Lerp(body.position, bone.Target.position, blend);
                Quaternion rotation = blend >= 0.999f
                    ? bone.Target.rotation
                    : Quaternion.Slerp(body.rotation, bone.Target.rotation, blend);

                body.position = position;
                body.rotation = rotation;
            }
        }

        private void CacheRecoveryStartPose()
        {
            recoveryStartPositions.Clear();
            recoveryStartRotations.Clear();

            for (int i = 0; i < boneBodies.Count; i++)
            {
                Rigidbody body = boneBodies[i];
                recoveryStartPositions.Add(body != null ? body.position : Vector3.zero);
                recoveryStartRotations.Add(body != null ? body.rotation : Quaternion.identity);
            }
        }

        private void BlendPhysicalBodyToAnimatedPose(float progress)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                ApexRagdollBone bone = bones[i];
                Rigidbody body = boneBodies[i];
                if (bone == null || bone.Target == null || body == null ||
                    i >= recoveryStartPositions.Count || i >= recoveryStartRotations.Count)
                {
                    continue;
                }

                body.position = Vector3.Lerp(
                    recoveryStartPositions[i],
                    bone.Target.position,
                    progress);
                body.rotation = Quaternion.Slerp(
                    recoveryStartRotations[i],
                    bone.Target.rotation,
                    progress);
            }
        }

        private void ApplyMotorMovement(float controlScale)
        {
            if (motorBody.isKinematic)
            {
                return;
            }

            Vector3 desiredVelocity = navigator != null
                ? Vector3.ProjectOnPlane(navigator.DesiredVelocity, Vector3.up)
                : Vector3.zero;
            desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, movementSpeed) * controlScale;

            Vector3 currentVelocity = Vector3.ProjectOnPlane(motorBody.velocity, Vector3.up);
            Vector3 velocityError = desiredVelocity - currentVelocity;
            float response = desiredVelocity.sqrMagnitude > 0.0025f ? acceleration : braking;
            Vector3 force = Vector3.ClampMagnitude(
                velocityError * response * motorBody.mass,
                motorBody.mass * response * movementSpeed);
            motorBody.AddForce(force, ForceMode.Force);

            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    desiredVelocity.normalized,
                    Vector3.up);
                Quaternion nextRotation = Quaternion.RotateTowards(
                    motorBody.rotation,
                    desiredRotation,
                    turnSpeed * Time.fixedDeltaTime);
                motorBody.MoveRotation(nextRotation);
            }
        }

        private bool CheckGrounded()
        {
            if (motorCollider == null)
            {
                return false;
            }

            Vector3 center = motorCollider.transform.TransformPoint(motorCollider.center);
            float bottom = center.y - motorCollider.height * 0.5f;
            Vector3 origin = new Vector3(center.x, bottom + motorCollider.radius + 0.05f, center.z);
            return Physics.SphereCast(
                origin,
                motorCollider.radius * 0.8f,
                Vector3.down,
                out _,
                0.18f,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private void PositionMotorUnderRagdoll()
        {
            if (motorBody == null || humanoid.PhysicalHips == null)
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            float groundHeight = ResolveGroundHeight(hips.position);
            Vector3 forward = ResolveRecoveryForward();
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 position = new Vector3(hips.position.x, groundHeight, hips.position.z);

            motorBody.position = position;
            motorBody.rotation = rotation;
            motorBody.velocity = Vector3.zero;
            motorBody.angularVelocity = Vector3.zero;

            if (hipsAnchor != null)
            {
                hipsAnchor.localPosition = new Vector3(0f, standingHipsHeight, 0f);
            }
        }

        private Vector3 ResolveRecoveryForward()
        {
            if (humanoid.PhysicalCharacter != null)
            {
                Animator physicalAnimator =
                    humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
                if (physicalAnimator != null && physicalAnimator.isHuman)
                {
                    Transform chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest != null)
                    {
                        Vector3 chestForward = Vector3.ProjectOnPlane(chest.forward, Vector3.up);
                        if (chestForward.sqrMagnitude > 0.0001f)
                        {
                            return chestForward.normalized;
                        }
                    }
                }
            }

            Vector3 fallback = motorBody != null
                ? Vector3.ProjectOnPlane(motorBody.transform.forward, Vector3.up)
                : Vector3.forward;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        private bool IsBodyFaceUp()
        {
            if (humanoid.PhysicalCharacter == null)
            {
                return true;
            }

            Animator physicalAnimator =
                humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (physicalAnimator == null || !physicalAnimator.isHuman)
            {
                return true;
            }

            Transform chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Chest);
            return chest == null || Vector3.Dot(chest.forward, Vector3.up) >= 0f;
        }

        private float CalculateCharacterHeight()
        {
            if (humanoid.PhysicalCharacter == null)
            {
                return 1.8f;
            }

            Animator animator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
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

        private float ResolveGroundHeight(Vector3 hipsPosition)
        {
            Vector3 origin = hipsPosition + Vector3.up * characterHeight;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                characterHeight * 3f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            float result = hipsPosition.y - standingHipsHeight;
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

        private void SubscribeToImpacts()
        {
            if (subscribed)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].Impacted += HandleBoneImpact;
                }
            }

            if (motorApexBody != null)
            {
                motorApexBody.Impacted += HandleMotorImpact;
            }

            subscribed = true;
        }

        private void UnsubscribeFromImpacts()
        {
            if (!subscribed)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].Impacted -= HandleBoneImpact;
                }
            }

            if (motorApexBody != null)
            {
                motorApexBody.Impacted -= HandleMotorImpact;
            }

            subscribed = false;
        }

        private void HandleBoneImpact(ApexRagdollBone bone, Collision collision)
        {
            if (!CanRagdollFromImpact() || collision == null)
            {
                return;
            }

            bool hardEnough = collision.relativeVelocity.magnitude >= minimumRagdollImpactSpeed ||
                              collision.impulse.magnitude >= minimumRagdollImpulse;
            if (hardEnough)
            {
                KnockDown(bone, collision);
            }
        }

        private void HandleMotorImpact(ApexImpactInfo impact)
        {
            if (!CanRagdollFromImpact())
            {
                return;
            }

            bool hardEnough = impact.Speed >= minimumRagdollImpactSpeed ||
                              impact.Impulse >= minimumRagdollImpulse;
            if (hardEnough)
            {
                KnockDown();
            }
        }

        private bool CanRagdollFromImpact()
        {
            return Time.time >= impactsArmedAt &&
                   (currentState == ApexPCCharacterState.Active ||
                    currentState == ApexPCCharacterState.Staggered);
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

        private void UpdateAnimatorParameters()
        {
            if (targetAnimator == null)
            {
                return;
            }

            float speed = motorBody != null
                ? Vector3.ProjectOnPlane(motorBody.velocity, Vector3.up).magnitude
                : 0f;
            bool moving = (currentState == ApexPCCharacterState.Active ||
                           currentState == ApexPCCharacterState.Staggered) && speed > 0.05f;

            SetAnimatorFloat(speedParameter, Mathf.Clamp01(speed / Mathf.Max(0.1f, movementSpeed)));
            SetAnimatorBool(movingParameter, moving);
            SetAnimatorBool(groundedParameter, grounded);
        }

        private void SetAnimatorFloat(string parameter, float value)
        {
            int hash = GetParameterHash(parameter, AnimatorControllerParameterType.Float);
            if (hash != 0)
            {
                targetAnimator.SetFloat(hash, value, 0.1f, Time.deltaTime);
            }
        }

        private void SetAnimatorBool(string parameter, bool value)
        {
            int hash = GetParameterHash(parameter, AnimatorControllerParameterType.Bool);
            if (hash != 0)
            {
                targetAnimator.SetBool(hash, value);
            }
        }

        private void SetAnimatorTrigger(string parameter)
        {
            int hash = GetParameterHash(parameter, AnimatorControllerParameterType.Trigger);
            if (hash != 0)
            {
                targetAnimator.ResetTrigger(hash);
                targetAnimator.SetTrigger(hash);
            }
        }

        private int GetParameterHash(string parameter, AnimatorControllerParameterType type)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return 0;
            }

            int hash = Animator.StringToHash(parameter);
            return animatorParameters.TryGetValue(hash, out AnimatorControllerParameterType found) &&
                   found == type
                ? hash
                : 0;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.1f, movementSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            motorMass = Mathf.Max(1f, motorMass);
            minimumRagdollImpactSpeed = Mathf.Max(0f, minimumRagdollImpactSpeed);
            minimumRagdollImpulse = Mathf.Max(0f, minimumRagdollImpulse);
            startupImpactDelay = Mathf.Max(0f, startupImpactDelay);
            impactImpulseMultiplier = Mathf.Max(0f, impactImpulseMultiplier);
            ragdollDuration = Mathf.Max(0.1f, ragdollDuration);
            getUpDuration = Mathf.Max(0.1f, getUpDuration);
        }
#endif
    }
}

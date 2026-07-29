using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Authoritative locomotion body for a converted physical NPC. While Active, the
    /// physical hips are rigidly linked to this body and the animated target follows its
    /// hips anchor. During ragdoll the root is released completely. During recovery the
    /// support body is placed beneath the fallen hips and lifted back to standing height.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidSupportRig : MonoBehaviour
    {
        [Header("Generated Support Body")]
        [SerializeField] private Rigidbody supportBody;
        [SerializeField] private CapsuleCollider bodyCollider;
        [SerializeField] private BoxCollider torsoCollider;
        [SerializeField] private SphereCollider legacyGroundCollider;
        [SerializeField] private ConfigurableJoint hipsJoint;
        [SerializeField] private Transform hipsAnchor;

        [Header("Humanoid Systems")]
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private ApexNPCNavigator navigator;
        [SerializeField] private ApexNPCMotor legacyNpcMotor;

        [Header("NPC Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 3.5f;
        [SerializeField, Min(0f)] private float acceleration = 24f;
        [SerializeField, Min(0f)] private float braking = 30f;
        [SerializeField, Min(0f)] private float maximumMoveForce = 1800f;
        [SerializeField, Min(0f)] private float turnResponsiveness = 10f;
        [SerializeField, Min(0f)] private float maximumTurnTorque = 900f;

        [Header("Support Shape")]
        [SerializeField, Min(0.01f)] private float bodyRadiusRatio = 0.13f;
        [SerializeField, Min(0.01f)] private float torsoWidthRatio = 0.30f;
        [SerializeField, Min(0.01f)] private float torsoDepthRatio = 0.18f;
        [SerializeField, Min(0.01f)] private float torsoHeightRatio = 0.25f;
        [SerializeField, Min(0f)] private float floorClearanceRatio = 0.01f;
        [SerializeField, Min(0.01f)] private float supportMass = 45f;
        [SerializeField] private bool disableLegacyNpcMotor = true;
        [SerializeField] private bool disableRagdollSelfCollision = true;

        [Header("Captured Standing Pose")]
        [SerializeField] private bool standingPoseCaptured;
        [SerializeField] private float standingHipsHeight = 0.95f;
        [SerializeField] private Vector3 standingHipsLocalOffset;
        [SerializeField] private Quaternion standingHipsLocalRotation = Quaternion.identity;

        private readonly List<Collider> supportColliders = new List<Collider>();
        private readonly RaycastHit[] groundHits = new RaycastHit[32];

        private RigidbodyConstraints supportedConstraints;
        private bool configured;
        private bool rootAttached;
        private bool locomotionEnabled = true;
        private bool recovering;
        private float recoveryStartedAt;
        private float recoveryDuration = 1.5f;
        private Vector3 recoveryStartPosition;
        private Vector3 recoveryTargetPosition;
        private Quaternion recoveryStartRotation;
        private Quaternion recoveryTargetRotation;
        private float characterHeight = 1.8f;
        private bool grounded;

        public Rigidbody SupportBody => supportBody;
        public Transform HipsAnchor => hipsAnchor;
        public ApexPhysicalHumanoid Humanoid => humanoid;
        public bool IsSupported => rootAttached && !recovering;
        public bool IsGrounded => grounded;
        public bool IsRecovering => recovering;
        public float CharacterHeight => characterHeight;
        public bool LocomotionEnabled => locomotionEnabled;

        private void Awake()
        {
            EnsureConfigured();
        }

        private void FixedUpdate()
        {
            if (!configured || supportBody == null)
            {
                return;
            }

            if (recovering)
            {
                UpdateRecoveryLift();
                return;
            }

            UpdateGrounded();

            if (!locomotionEnabled || !rootAttached || supportBody.isKinematic)
            {
                return;
            }

            if (activeRagdoll != null && activeRagdoll.State != ApexRagdollState.Active)
            {
                return;
            }

            ApplyNpcMovement();
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            humanoid = owner;
            activeRagdoll = owner != null ? owner.ActiveRagdoll : null;
            navigator = owner != null ? owner.NPCNavigator : null;
            legacyNpcMotor = owner != null ? owner.NPCMotor : null;

            BuildOrRepairSupportBody();
            AttachRootToSupport();
            ConfigureTargetRootDriver();
            ConfigureCollisionFiltering();
            DisableLegacyLocoball();

            if (disableLegacyNpcMotor && legacyNpcMotor != null)
            {
                legacyNpcMotor.enabled = false;
            }

            configured = supportBody != null && hipsJoint != null && hipsAnchor != null;
        }

        public void RebuildSupport()
        {
            configured = false;
            if (!Application.isPlaying)
            {
                standingPoseCaptured = false;
            }

            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
        }

        public void SnapSupportToHumanoid()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            float groundHeight = ResolveGroundHeight(hips.position);
            Quaternion uprightRotation = GetUprightRotation(hips);

            SetSupportPose(hips.position, uprightRotation);
            CaptureStandingPose(hips, groundHeight, true);
            ConfigureSupportGeometry(groundHeight);
            AttachRootToSupport();
            ConfigureTargetRootDriver();
            EnableSupportColliders(true);
            grounded = true;
        }

        public void SetLocomotionEnabled(bool enabled)
        {
            locomotionEnabled = enabled;
            if (!enabled && supportBody != null && !supportBody.isKinematic)
            {
                Vector3 verticalVelocity = Vector3.Project(supportBody.velocity, Vector3.up);
                supportBody.velocity = verticalVelocity;
                supportBody.angularVelocity = Vector3.zero;
            }
        }

        public void ReleaseForRagdoll()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            locomotionEnabled = false;
            recovering = false;
            rootAttached = false;

            ConfigureHipsJointReleased();
            EnableSupportColliders(false);

            supportBody.velocity = Vector3.zero;
            supportBody.angularVelocity = Vector3.zero;
            supportBody.useGravity = false;
            supportBody.isKinematic = true;
            supportBody.constraints = RigidbodyConstraints.None;
        }

        public void BeginRecovery(float duration)
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            float groundHeight = ResolveGroundHeight(hips.position);
            Quaternion uprightRotation = GetUprightRotation(hips);

            recoveryDuration = Mathf.Max(0.1f, duration);
            recoveryStartedAt = Time.time;
            recoveryStartPosition = hips.position;
            recoveryStartRotation = uprightRotation;
            recoveryTargetPosition = new Vector3(
                hips.position.x,
                groundHeight + Mathf.Max(characterHeight * 0.42f, standingHipsHeight),
                hips.position.z);
            recoveryTargetRotation = uprightRotation;

            supportBody.isKinematic = true;
            supportBody.useGravity = false;
            supportBody.constraints = RigidbodyConstraints.None;
            SetSupportPose(recoveryStartPosition, recoveryStartRotation);

            ConfigureHipsAnchor();
            AttachRootToSupport();
            ConfigureTargetRootDriver();
            EnableSupportColliders(false);

            locomotionEnabled = false;
            recovering = true;
            grounded = false;
        }

        public void FinishRecovery()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            if (recovering)
            {
                SetSupportPose(recoveryTargetPosition, recoveryTargetRotation);
            }

            recovering = false;
            AttachRootToSupport();
            ConfigureSupportGeometry(ResolveGroundHeight(supportBody.position));
            EnableSupportColliders(true);

            supportBody.isKinematic = false;
            supportBody.useGravity = true;
            supportBody.constraints = supportedConstraints;
            supportBody.velocity = Vector3.zero;
            supportBody.angularVelocity = Vector3.zero;

            locomotionEnabled = true;
            grounded = true;
        }

        private void UpdateRecoveryLift()
        {
            float t = Mathf.Clamp01((Time.time - recoveryStartedAt) / recoveryDuration);
            float smooth = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(recoveryStartPosition, recoveryTargetPosition, smooth);
            Quaternion rotation = Quaternion.Slerp(recoveryStartRotation, recoveryTargetRotation, smooth);

            supportBody.MovePosition(position);
            supportBody.MoveRotation(rotation);

            if (t >= 1f)
            {
                grounded = true;
            }
        }

        private void EnsureConfigured()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            if (!configured && humanoid != null)
            {
                Configure(humanoid);
            }
        }

        private bool ResolveRequiredReferences()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            if (humanoid == null || humanoid.PhysicalHips == null)
            {
                return false;
            }

            if (supportBody == null)
            {
                BuildOrRepairSupportBody();
            }

            if (hipsJoint == null)
            {
                hipsJoint = humanoid.PhysicalHips.Joint;
            }

            return supportBody != null && hipsJoint != null;
        }

        private void BuildOrRepairSupportBody()
        {
            if (humanoid == null || humanoid.PhysicalHips == null)
            {
                return;
            }

            characterHeight = CalculateCharacterHeight();
            Transform hips = humanoid.PhysicalHips.transform;
            float groundHeight = ResolveGroundHeight(hips.position);
            Quaternion uprightRotation = GetUprightRotation(hips);

            Transform supportTransform = transform.Find("Apex Humanoid Support Body");
            if (supportTransform == null)
            {
                GameObject supportObject = new GameObject("Apex Humanoid Support Body");
                supportTransform = supportObject.transform;
                supportTransform.SetParent(transform, true);
            }

            supportTransform.localScale = Vector3.one;
            supportTransform.SetPositionAndRotation(hips.position, uprightRotation);

            supportBody = GetOrAdd<Rigidbody>(supportTransform.gameObject);
            supportBody.mass = supportMass;
            supportBody.useGravity = true;
            supportBody.isKinematic = false;
            supportBody.interpolation = RigidbodyInterpolation.Interpolate;
            supportBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            supportBody.solverIterations = 16;
            supportBody.solverVelocityIterations = 8;
            supportedConstraints = RigidbodyConstraints.FreezeRotationX |
                                   RigidbodyConstraints.FreezeRotationZ;
            supportBody.constraints = supportedConstraints;

            GetOrAdd<ApexBody>(supportTransform.gameObject);

            bodyCollider = GetOrAdd<CapsuleCollider>(supportTransform.gameObject);
            bodyCollider.direction = 1;

            torsoCollider = GetOrAdd<BoxCollider>(supportTransform.gameObject);
            legacyGroundCollider = GetOrAdd<SphereCollider>(supportTransform.gameObject);
            legacyGroundCollider.enabled = false;

            if (!standingPoseCaptured || !Application.isPlaying)
            {
                CaptureStandingPose(hips, groundHeight, false);
            }

            ConfigureSupportGeometry(groundHeight);

            hipsAnchor = supportTransform.Find("Hips Anchor");
            if (hipsAnchor == null)
            {
                GameObject anchorObject = new GameObject("Hips Anchor");
                hipsAnchor = anchorObject.transform;
                hipsAnchor.SetParent(supportTransform, false);
            }

            ConfigureHipsAnchor();
        }

        private void CaptureStandingPose(Transform hips, float groundHeight, bool force)
        {
            if (standingPoseCaptured && !force)
            {
                return;
            }

            standingHipsHeight = Mathf.Max(characterHeight * 0.42f, hips.position.y - groundHeight);
            standingHipsLocalOffset = supportBody != null
                ? supportBody.transform.InverseTransformPoint(hips.position)
                : Vector3.zero;
            standingHipsLocalRotation = supportBody != null
                ? Quaternion.Inverse(supportBody.rotation) * hips.rotation
                : Quaternion.identity;
            standingPoseCaptured = true;
        }

        private void ConfigureSupportGeometry(float groundHeight)
        {
            if (supportBody == null || bodyCollider == null || torsoCollider == null)
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            float radius = Mathf.Max(0.06f, characterHeight * bodyRadiusRatio);
            float clearance = characterHeight * floorClearanceRatio;
            float bottom = groundHeight + clearance;
            float top = Mathf.Max(
                hips.position.y + characterHeight * 0.24f,
                bottom + radius * 2f);
            float height = Mathf.Max(radius * 2f, top - bottom);
            Vector3 capsuleWorldCenter = new Vector3(
                hips.position.x,
                (bottom + top) * 0.5f,
                hips.position.z);

            bodyCollider.radius = radius;
            bodyCollider.height = height;
            bodyCollider.center = supportBody.transform.InverseTransformPoint(capsuleWorldCenter);
            bodyCollider.enabled = true;

            Transform chest = ResolveChest();
            Vector3 torsoWorldCenter = chest != null
                ? chest.position
                : hips.position + Vector3.up * characterHeight * 0.18f;
            torsoCollider.center = supportBody.transform.InverseTransformPoint(torsoWorldCenter);
            torsoCollider.size = new Vector3(
                characterHeight * torsoWidthRatio,
                characterHeight * torsoHeightRatio,
                characterHeight * torsoDepthRatio);
            torsoCollider.enabled = true;

            supportBody.centerOfMass = supportBody.transform.InverseTransformPoint(
                hips.position - Vector3.up * characterHeight * 0.18f);

            supportColliders.Clear();
            supportColliders.Add(bodyCollider);
            supportColliders.Add(torsoCollider);
        }

        private void AttachRootToSupport()
        {
            if (humanoid == null || humanoid.PhysicalHips == null || supportBody == null)
            {
                return;
            }

            hipsJoint = humanoid.PhysicalHips.Joint;
            if (hipsJoint == null)
            {
                hipsJoint = GetOrAdd<ConfigurableJoint>(humanoid.PhysicalHips.gameObject);
            }

            hipsJoint.connectedBody = supportBody;
            hipsJoint.autoConfigureConnectedAnchor = false;
            hipsJoint.anchor = Vector3.zero;
            hipsJoint.connectedAnchor = standingHipsLocalOffset;
            hipsJoint.configuredInWorldSpace = false;
            hipsJoint.xMotion = ConfigurableJointMotion.Locked;
            hipsJoint.yMotion = ConfigurableJointMotion.Locked;
            hipsJoint.zMotion = ConfigurableJointMotion.Locked;
            hipsJoint.angularXMotion = ConfigurableJointMotion.Locked;
            hipsJoint.angularYMotion = ConfigurableJointMotion.Locked;
            hipsJoint.angularZMotion = ConfigurableJointMotion.Locked;
            hipsJoint.rotationDriveMode = RotationDriveMode.Slerp;
            hipsJoint.enableCollision = false;
            hipsJoint.enablePreprocessing = false;
            hipsJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            hipsJoint.projectionDistance = 0.03f;
            hipsJoint.projectionAngle = 5f;

            rootAttached = true;
        }

        private void ConfigureHipsJointReleased()
        {
            if (hipsJoint == null)
            {
                return;
            }

            hipsJoint.xMotion = ConfigurableJointMotion.Free;
            hipsJoint.yMotion = ConfigurableJointMotion.Free;
            hipsJoint.zMotion = ConfigurableJointMotion.Free;
            hipsJoint.angularXMotion = ConfigurableJointMotion.Free;
            hipsJoint.angularYMotion = ConfigurableJointMotion.Free;
            hipsJoint.angularZMotion = ConfigurableJointMotion.Free;
            hipsJoint.projectionMode = JointProjectionMode.None;
        }

        private void ConfigureHipsAnchor()
        {
            if (hipsAnchor == null)
            {
                return;
            }

            hipsAnchor.localPosition = standingHipsLocalOffset;
            hipsAnchor.localRotation = standingHipsLocalRotation;
            hipsAnchor.localScale = Vector3.one;
        }

        private void ConfigureTargetRootDriver()
        {
            if (humanoid == null || humanoid.TargetRootDriver == null ||
                humanoid.TargetHips == null || hipsAnchor == null)
            {
                return;
            }

            humanoid.TargetRootDriver.Configure(
                hipsAnchor,
                humanoid.TargetHips,
                true,
                true,
                true);
        }

        private void ConfigureCollisionFiltering()
        {
            IgnoreSupportBodyCollisions();

            if (disableRagdollSelfCollision && humanoid != null && humanoid.CollisionFilter != null)
            {
                humanoid.CollisionFilter.SetIgnoreAllSelfCollisions(true);
            }
        }

        private void IgnoreSupportBodyCollisions()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            Collider[] physicalColliders =
                humanoid.PhysicalCharacter.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < supportColliders.Count; i++)
            {
                Collider supportCollider = supportColliders[i];
                if (supportCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < physicalColliders.Length; j++)
                {
                    Collider physicalCollider = physicalColliders[j];
                    if (physicalCollider != null && physicalCollider != supportCollider)
                    {
                        Physics.IgnoreCollision(supportCollider, physicalCollider, true);
                    }
                }
            }
        }

        private void DisableLegacyLocoball()
        {
            if (supportBody == null)
            {
                return;
            }

            Transform legacyLocoball = supportBody.transform.Find("Apex Humanoid Locoball");
            if (legacyLocoball == null)
            {
                return;
            }

            Collider[] colliders = legacyLocoball.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private void EnableSupportColliders(bool enabled)
        {
            for (int i = 0; i < supportColliders.Count; i++)
            {
                if (supportColliders[i] != null)
                {
                    supportColliders[i].enabled = enabled;
                }
            }
        }

        private void ApplyNpcMovement()
        {
            if (humanoid == null || humanoid.Mode != ApexPhysicalHumanoidMode.PhysicalNPC ||
                navigator == null || supportBody == null)
            {
                return;
            }

            Vector3 desiredVelocity = CalculateRequestedVelocity();
            Vector3 currentVelocity = Vector3.ProjectOnPlane(supportBody.velocity, Vector3.up);
            Vector3 velocityError = desiredVelocity - currentVelocity;
            float response = desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : braking;
            Vector3 force = Vector3.ClampMagnitude(
                velocityError * response * supportBody.mass,
                maximumMoveForce);
            supportBody.AddForce(force, ForceMode.Force);

            Vector3 direction = Vector3.ProjectOnPlane(desiredVelocity, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 currentForward = Vector3.ProjectOnPlane(supportBody.transform.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.0001f)
            {
                currentForward = Vector3.forward;
            }

            float angleError = Vector3.SignedAngle(currentForward, direction, Vector3.up);
            float currentYawVelocity = Vector3.Dot(supportBody.angularVelocity, Vector3.up);
            float desiredYawVelocity = angleError * Mathf.Deg2Rad * turnResponsiveness;
            float torque = Mathf.Clamp(
                (desiredYawVelocity - currentYawVelocity) * supportBody.mass * turnResponsiveness,
                -maximumTurnTorque,
                maximumTurnTorque);
            supportBody.AddTorque(Vector3.up * torque, ForceMode.Force);
        }

        private Vector3 CalculateRequestedVelocity()
        {
            if (navigator == null || !navigator.HasDestination || navigator.HasReachedDestination ||
                navigator.IsPathPending || !navigator.HasPath)
            {
                return Vector3.zero;
            }

            Vector3 direction = navigator.SteeringTarget - supportBody.position;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.ProjectOnPlane(navigator.DesiredVelocity, Vector3.up);
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            float stoppingDistance = navigator.Agent != null
                ? Mathf.Max(0.1f, navigator.Agent.stoppingDistance * 2f)
                : 1f;
            float speedMultiplier = Mathf.Clamp01(navigator.RemainingDistance / stoppingDistance);
            return direction.normalized * movementSpeed * speedMultiplier;
        }

        private void UpdateGrounded()
        {
            if (supportBody == null)
            {
                grounded = false;
                return;
            }

            float groundHeight = ResolveGroundHeight(supportBody.position);
            float expectedHipsY = groundHeight + standingHipsHeight;
            grounded = Mathf.Abs(supportBody.position.y - expectedHipsY) <= characterHeight * 0.2f;
        }

        private Quaternion GetUprightRotation(Transform hips)
        {
            Vector3 forward = hips != null
                ? Vector3.ProjectOnPlane(hips.forward, Vector3.up)
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private float ResolveGroundHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * characterHeight;
            float distance = characterHeight * 3f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            float groundHeight = position.y - characterHeight * 0.55f;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || IsInternalCollider(hit.collider))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    groundHeight = hit.point.y;
                    found = true;
                }
            }

            if (found)
            {
                return groundHeight;
            }

            if (TryGetFootSoleHeight(out float soleHeight))
            {
                return soleHeight;
            }

            return groundHeight;
        }

        private bool IsInternalCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            if (supportBody != null && candidate.transform.IsChildOf(supportBody.transform))
            {
                return true;
            }

            return humanoid != null && candidate.transform.IsChildOf(humanoid.transform);
        }

        private bool TryGetFootSoleHeight(out float soleHeight)
        {
            soleHeight = 0f;
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return false;
            }

            Animator animator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return false;
            }

            float lowest = float.PositiveInfinity;
            IncludeFoot(animator.GetBoneTransform(HumanBodyBones.LeftFoot), ref lowest);
            IncludeFoot(animator.GetBoneTransform(HumanBodyBones.RightFoot), ref lowest);
            if (float.IsPositiveInfinity(lowest))
            {
                return false;
            }

            soleHeight = lowest;
            return true;
        }

        private void IncludeFoot(Transform foot, ref float lowest)
        {
            if (foot == null)
            {
                return;
            }

            Collider footCollider = foot.GetComponent<Collider>();
            float candidate = footCollider != null
                ? footCollider.bounds.min.y
                : foot.position.y - characterHeight * 0.035f;
            lowest = Mathf.Min(lowest, candidate);
        }

        private Transform ResolveChest()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return null;
            }

            Animator animator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
            {
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            if (chest == null)
            {
                chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            return chest;
        }

        private float CalculateCharacterHeight()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return 1.8f;
            }

            Animator animator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (head != null && (leftFoot != null || rightFoot != null))
                {
                    Vector3 feet = leftFoot != null && rightFoot != null
                        ? (leftFoot.position + rightFoot.position) * 0.5f
                        : (leftFoot != null ? leftFoot.position : rightFoot.position);
                    float measured = Vector3.Dot(head.position - feet, Vector3.up);
                    if (measured > 0.5f)
                    {
                        return measured * 1.08f;
                    }
                }
            }

            Renderer[] renderers = humanoid.PhysicalCharacter.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 1.8f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(0.5f, bounds.size.y);
        }

        private void SetSupportPose(Vector3 position, Quaternion rotation)
        {
            if (supportBody == null)
            {
                return;
            }

            supportBody.position = position;
            supportBody.rotation = rotation;
            supportBody.velocity = Vector3.zero;
            supportBody.angularVelocity = Vector3.zero;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            maximumMoveForce = Mathf.Max(0f, maximumMoveForce);
            turnResponsiveness = Mathf.Max(0f, turnResponsiveness);
            maximumTurnTorque = Mathf.Max(0f, maximumTurnTorque);
            bodyRadiusRatio = Mathf.Max(0.01f, bodyRadiusRatio);
            torsoWidthRatio = Mathf.Max(0.01f, torsoWidthRatio);
            torsoDepthRatio = Mathf.Max(0.01f, torsoDepthRatio);
            torsoHeightRatio = Mathf.Max(0.01f, torsoHeightRatio);
            floorClearanceRatio = Mathf.Max(0f, floorClearanceRatio);
            supportMass = Mathf.Max(0.01f, supportMass);
        }

        private void OnDrawGizmosSelected()
        {
            if (supportBody == null || hipsAnchor == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(hipsAnchor.position, characterHeight * 0.035f);
        }
#endif
    }
}

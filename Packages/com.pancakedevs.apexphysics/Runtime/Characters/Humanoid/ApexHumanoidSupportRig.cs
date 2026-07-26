using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Stable hidden physics core for a fully articulated humanoid. The support body
    /// handles navigation, ground contact, and upright stability while the visible
    /// skeleton remains physical and can still knock down or recover.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidSupportRig : MonoBehaviour
    {
        [Header("Generated Support Body")]
        [SerializeField] private Rigidbody supportBody;
        [SerializeField] private BoxCollider torsoCollider;
        [SerializeField] private CapsuleCollider lowerBodyCollider;
        [SerializeField] private SphereCollider groundCollider;
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

        [Header("Support")]
        [SerializeField, Range(0.1f, 1f)] private float supportHeightRatio = 0.86f;
        [SerializeField, Min(0.01f)] private float supportRadiusRatio = 0.115f;
        [SerializeField, Min(0.01f)] private float torsoWidthRatio = 0.30f;
        [SerializeField, Min(0.01f)] private float torsoDepthRatio = 0.18f;
        [SerializeField, Min(0.01f)] private float torsoHeightRatio = 0.28f;
        [SerializeField, Min(0f)] private float floorClearanceRatio = 0.012f;
        [SerializeField, Min(0.01f)] private float supportMass = 45f;
        [SerializeField] private bool disableLegacyNpcMotor = true;
        [SerializeField] private bool disableRagdollSelfCollision = true;

        [Header("Standing Pose")]
        [SerializeField] private bool standingOffsetCaptured;
        [SerializeField] private Vector3 standingHipsLocalOffset;

        private readonly List<Collider> supportColliders = new List<Collider>();
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private RigidbodyConstraints supportedConstraints;
        private bool subscribed;
        private bool configured;
        private float characterHeight = 1.8f;

        public Rigidbody SupportBody => supportBody;
        public Transform HipsAnchor => hipsAnchor;
        public ApexPhysicalHumanoid Humanoid => humanoid;
        public bool IsSupported => activeRagdoll == null || activeRagdoll.State != ApexRagdollState.Limp;
        public float CharacterHeight => characterHeight;

        private void Awake()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            EnsureConfigured();
            Subscribe();
            ApplyState(activeRagdoll != null ? activeRagdoll.State : ApexRagdollState.Active, true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void FixedUpdate()
        {
            if (!configured || supportBody == null)
            {
                return;
            }

            float controlStrength = GetControlStrength();
            if (controlStrength <= 0f)
            {
                return;
            }

            ApplyNpcMovement(controlStrength);
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            humanoid = owner;
            activeRagdoll = owner != null ? owner.ActiveRagdoll : null;
            navigator = owner != null ? owner.NPCNavigator : null;
            legacyNpcMotor = owner != null ? owner.NPCMotor : null;

            BuildOrRepairSupportBody();
            ConfigureHipsJoint();
            ConfigureTargetRootDriver();
            ConfigureCollisionFiltering();
            configured = supportBody != null && hipsJoint != null && hipsAnchor != null;

            if (disableLegacyNpcMotor && legacyNpcMotor != null)
            {
                legacyNpcMotor.enabled = false;
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
                ApplyState(activeRagdoll != null ? activeRagdoll.State : ApexRagdollState.Active, true);
            }
        }

        public void RebuildSupport()
        {
            configured = false;
            if (!Application.isPlaying)
            {
                standingOffsetCaptured = false;
            }

            Configure(humanoid != null ? humanoid : GetComponent<ApexPhysicalHumanoid>());
        }

        public void SnapSupportToHumanoid()
        {
            if (supportBody == null || humanoid == null || humanoid.PhysicalHips == null)
            {
                return;
            }

            Transform hips = humanoid.PhysicalHips.transform;
            Quaternion uprightRotation = GetUprightRotation(hips);
            float groundHeight = ResolveGroundHeight(hips.position);
            float lowestLocalPoint = GetLowestSupportLocalY();

            Vector3 supportPosition = hips.position;
            supportPosition.y = groundHeight - lowestLocalPoint + characterHeight * floorClearanceRatio;

            supportBody.position = supportPosition;
            supportBody.rotation = uprightRotation;
            supportBody.velocity = Vector3.zero;
            supportBody.angularVelocity = Vector3.zero;

            if (!standingOffsetCaptured)
            {
                standingHipsLocalOffset = supportBody.transform.InverseTransformPoint(hips.position);
                standingOffsetCaptured = true;
            }

            ConfigureHipsAnchor();
            ConfigureJointAnchors();
            humanoid.TargetRootDriver?.SnapNow();
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

        private void BuildOrRepairSupportBody()
        {
            if (humanoid == null || humanoid.PhysicalHips == null)
            {
                return;
            }

            characterHeight = CalculateCharacterHeight();

            Transform supportTransform = transform.Find("Apex Humanoid Support Body");
            if (supportTransform == null)
            {
                GameObject supportObject = new GameObject("Apex Humanoid Support Body");
                supportTransform = supportObject.transform;
                supportTransform.SetParent(transform, true);
            }

            supportTransform.localScale = Vector3.one;
            supportBody = GetOrAdd<Rigidbody>(supportTransform.gameObject);
            supportBody.mass = supportMass;
            supportBody.useGravity = true;
            supportBody.isKinematic = false;
            supportBody.interpolation = RigidbodyInterpolation.Interpolate;
            supportBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            supportBody.centerOfMass = Vector3.down * characterHeight * 0.18f;
            supportBody.solverIterations = 16;
            supportBody.solverVelocityIterations = 8;
            supportedConstraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            GetOrAdd<ApexBody>(supportTransform.gameObject);

            torsoCollider = GetOrAdd<BoxCollider>(supportTransform.gameObject);
            torsoCollider.center = Vector3.up * characterHeight * 0.12f;
            torsoCollider.size = new Vector3(
                characterHeight * torsoWidthRatio,
                characterHeight * torsoHeightRatio,
                characterHeight * torsoDepthRatio);

            lowerBodyCollider = GetOrAdd<CapsuleCollider>(supportTransform.gameObject);
            lowerBodyCollider.direction = 1;
            lowerBodyCollider.radius = characterHeight * supportRadiusRatio;
            lowerBodyCollider.height = Mathf.Max(
                lowerBodyCollider.radius * 2f,
                characterHeight * supportHeightRatio);
            lowerBodyCollider.center = Vector3.down * characterHeight * 0.24f;

            groundCollider = GetOrAdd<SphereCollider>(supportTransform.gameObject);
            groundCollider.radius = characterHeight * supportRadiusRatio * 0.95f;
            groundCollider.center = Vector3.down * characterHeight * 0.52f;

            supportColliders.Clear();
            supportColliders.Add(torsoCollider);
            supportColliders.Add(lowerBodyCollider);
            supportColliders.Add(groundCollider);

            Transform hips = humanoid.PhysicalHips.transform;
            Quaternion uprightRotation = GetUprightRotation(hips);
            float groundHeight = ResolveGroundHeight(hips.position);
            float lowestLocalPoint = GetLowestSupportLocalY();
            Vector3 supportPosition = hips.position;
            supportPosition.y = groundHeight - lowestLocalPoint + characterHeight * floorClearanceRatio;
            supportTransform.SetPositionAndRotation(supportPosition, uprightRotation);

            if (!standingOffsetCaptured || !Application.isPlaying)
            {
                standingHipsLocalOffset = supportTransform.InverseTransformPoint(hips.position);
                standingOffsetCaptured = true;
            }

            hipsAnchor = supportTransform.Find("Hips Anchor");
            if (hipsAnchor == null)
            {
                GameObject anchorObject = new GameObject("Hips Anchor");
                hipsAnchor = anchorObject.transform;
                hipsAnchor.SetParent(supportTransform, false);
            }

            ConfigureHipsAnchor();
        }

        private void ConfigureHipsJoint()
        {
            if (humanoid == null || humanoid.PhysicalHips == null || supportBody == null)
            {
                return;
            }

            humanoid.PhysicalHips.SetMuscleMultiplier(1f);
            hipsJoint = humanoid.PhysicalHips.Joint;
            if (hipsJoint == null)
            {
                hipsJoint = GetOrAdd<ConfigurableJoint>(humanoid.PhysicalHips.gameObject);
            }

            hipsJoint.connectedBody = supportBody;
            hipsJoint.autoConfigureConnectedAnchor = false;
            hipsJoint.anchor = Vector3.zero;
            ConfigureJointAnchors();
            hipsJoint.configuredInWorldSpace = false;
            hipsJoint.xMotion = ConfigurableJointMotion.Locked;
            hipsJoint.yMotion = ConfigurableJointMotion.Locked;
            hipsJoint.zMotion = ConfigurableJointMotion.Locked;
            hipsJoint.angularXMotion = ConfigurableJointMotion.Limited;
            hipsJoint.angularYMotion = ConfigurableJointMotion.Limited;
            hipsJoint.angularZMotion = ConfigurableJointMotion.Limited;
            hipsJoint.lowAngularXLimit = CreateLimit(-35f);
            hipsJoint.highAngularXLimit = CreateLimit(35f);
            hipsJoint.angularYLimit = CreateLimit(45f);
            hipsJoint.angularZLimit = CreateLimit(35f);
            hipsJoint.rotationDriveMode = RotationDriveMode.Slerp;
            hipsJoint.enableCollision = false;
            hipsJoint.enablePreprocessing = false;
            hipsJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            hipsJoint.projectionDistance = 0.04f;
            hipsJoint.projectionAngle = 8f;
        }

        private void ConfigureJointAnchors()
        {
            if (hipsJoint == null || supportBody == null)
            {
                return;
            }

            hipsJoint.connectedAnchor = standingHipsLocalOffset;
        }

        private void ConfigureHipsAnchor()
        {
            if (hipsAnchor == null)
            {
                return;
            }

            hipsAnchor.localPosition = standingHipsLocalOffset;
            hipsAnchor.localRotation = Quaternion.identity;
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
                true);
            humanoid.TargetRootDriver.SnapNow();
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

            Collider[] physicalColliders = humanoid.PhysicalCharacter.GetComponentsInChildren<Collider>(true);
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

        private void ApplyNpcMovement(float strength)
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
                maximumMoveForce) * strength;
            supportBody.AddForce(force, ForceMode.Force);

            Vector3 direction = Vector3.ProjectOnPlane(desiredVelocity, Vector3.up);
            if (direction.sqrMagnitude > 0.0001f)
            {
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
                supportBody.AddTorque(Vector3.up * torque * strength, ForceMode.Force);
            }
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

        private float GetControlStrength()
        {
            if (activeRagdoll == null)
            {
                return 1f;
            }

            switch (activeRagdoll.State)
            {
                case ApexRagdollState.Active:
                    return 1f;
                case ApexRagdollState.Recovering:
                    return activeRagdoll.RecoveryProgress;
                default:
                    return 0f;
            }
        }

        private void Subscribe()
        {
            if (subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged -= HandleStateChanged;
            subscribed = false;
        }

        private void HandleStateChanged(ApexRagdollState state)
        {
            ApplyState(state, false);
        }

        private void ApplyState(ApexRagdollState state, bool immediate)
        {
            if (supportBody == null)
            {
                return;
            }

            if (state == ApexRagdollState.Limp)
            {
                supportBody.constraints = RigidbodyConstraints.None;
                if (legacyNpcMotor != null)
                {
                    legacyNpcMotor.enabled = false;
                }

                return;
            }

            if (state == ApexRagdollState.Recovering || immediate)
            {
                SnapSupportToHumanoid();
            }

            supportBody.constraints = supportedConstraints;
            if (disableLegacyNpcMotor && legacyNpcMotor != null)
            {
                legacyNpcMotor.enabled = false;
            }
        }

        private Quaternion GetUprightRotation(Transform hips)
        {
            Vector3 forward = hips != null
                ? Vector3.ProjectOnPlane(hips.forward, Vector3.up)
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
                forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private float ResolveGroundHeight(Vector3 hipsPosition)
        {
            if (TryFindExternalGroundHeight(hipsPosition, out float groundHeight))
            {
                return groundHeight;
            }

            if (TryGetFootSoleHeight(out float soleHeight))
            {
                return soleHeight;
            }

            return hipsPosition.y - characterHeight * 0.55f;
        }

        private bool TryFindExternalGroundHeight(Vector3 hipsPosition, out float groundHeight)
        {
            Vector3 origin = hipsPosition + Vector3.up * characterHeight;
            float distance = characterHeight * 3f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            groundHeight = 0f;
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

            return found;
        }

        private bool IsInternalCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            if (supportColliders.Contains(candidate))
            {
                return true;
            }

            if (humanoid != null && humanoid.PhysicalCharacter != null &&
                candidate.transform.IsChildOf(humanoid.PhysicalCharacter.transform))
            {
                return true;
            }

            return humanoid != null && humanoid.AnimatedTargetCharacter != null &&
                   candidate.transform.IsChildOf(humanoid.AnimatedTargetCharacter.transform);
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

            bool found = false;
            float lowest = float.PositiveInfinity;
            IncludeFoot(animator.GetBoneTransform(HumanBodyBones.LeftFoot), ref found, ref lowest);
            IncludeFoot(animator.GetBoneTransform(HumanBodyBones.RightFoot), ref found, ref lowest);
            if (!found)
            {
                return false;
            }

            soleHeight = lowest;
            return true;
        }

        private void IncludeFoot(Transform foot, ref bool found, ref float lowest)
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
            found = true;
        }

        private float GetLowestSupportLocalY()
        {
            float lowest = 0f;
            if (torsoCollider != null)
            {
                lowest = Mathf.Min(lowest, torsoCollider.center.y - torsoCollider.size.y * 0.5f);
            }

            if (lowerBodyCollider != null)
            {
                lowest = Mathf.Min(lowest, lowerBodyCollider.center.y - lowerBodyCollider.height * 0.5f);
            }

            if (groundCollider != null)
            {
                lowest = Mathf.Min(lowest, groundCollider.center.y - groundCollider.radius);
            }

            return lowest;
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
                    Vector3 footPosition = leftFoot != null && rightFoot != null
                        ? (leftFoot.position + rightFoot.position) * 0.5f
                        : (leftFoot != null ? leftFoot.position : rightFoot.position);
                    float measuredHeight = Vector3.Dot(head.position - footPosition, Vector3.up);
                    if (measuredHeight > 0.5f)
                    {
                        return measuredHeight * 1.08f;
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

        private static SoftJointLimit CreateLimit(float angle)
        {
            return new SoftJointLimit
            {
                limit = angle,
                bounciness = 0f,
                contactDistance = 2f
            };
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
            supportHeightRatio = Mathf.Clamp(supportHeightRatio, 0.1f, 1f);
            supportRadiusRatio = Mathf.Max(0.01f, supportRadiusRatio);
            torsoWidthRatio = Mathf.Max(0.01f, torsoWidthRatio);
            torsoDepthRatio = Mathf.Max(0.01f, torsoDepthRatio);
            torsoHeightRatio = Mathf.Max(0.01f, torsoHeightRatio);
            floorClearanceRatio = Mathf.Max(0f, floorClearanceRatio);
            supportMass = Mathf.Max(0.01f, supportMass);
        }

        private void OnDrawGizmosSelected()
        {
            if (supportBody == null)
            {
                return;
            }

            Gizmos.matrix = supportBody.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(
                Vector3.up * characterHeight * 0.12f,
                new Vector3(
                    characterHeight * torsoWidthRatio,
                    characterHeight * torsoHeightRatio,
                    characterHeight * torsoDepthRatio));
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}

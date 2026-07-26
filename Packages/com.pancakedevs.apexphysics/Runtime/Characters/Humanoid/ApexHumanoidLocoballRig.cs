using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Adds a floor-aligned locoball and soft foot tethers to a supported physical
    /// humanoid. The support Rigidbody owns locomotion while the articulated feet
    /// remain close enough to the ground core to avoid stretching or flying away.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidLocoballRig : MonoBehaviour
    {
        [Header("Generated Locoball")]
        [SerializeField] private Transform locoball;
        [SerializeField] private SphereCollider locoballCollider;
        [SerializeField] private Transform leftFootAnchor;
        [SerializeField] private Transform rightFootAnchor;
        [SerializeField] private ApexHumanoidFootTether leftFootTether;
        [SerializeField] private ApexHumanoidFootTether rightFootTether;

        [Header("Humanoid Systems")]
        [SerializeField] private ApexPhysicalHumanoid humanoid;
        [SerializeField] private ApexHumanoidSupportRig supportRig;
        [SerializeField] private Rigidbody supportBody;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;

        [Header("Locoball Shape")]
        [SerializeField, Min(0.01f)] private float radiusRatio = 0.12f;
        [SerializeField, Min(0f)] private float floorClearanceRatio = 0.01f;
        [SerializeField, Min(0f)] private float footSpreadRatio = 0.09f;

        [Header("Foot Tethers")]
        [SerializeField, Min(0f)] private float tetherSpring = 1800f;
        [SerializeField, Min(0f)] private float tetherDamper = 120f;
        [SerializeField, Min(0f)] private float tetherDistanceRatio = 0.16f;
        [SerializeField, Min(0f)] private float limpSlackRatio = 1.1f;

        [Header("Startup Safety")]
        [SerializeField, Min(0f)] private float impactArmingDelay = 1f;

        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private Animator physicalAnimator;
        private Transform physicalHips;
        private Transform leftFoot;
        private Transform rightFoot;
        private float characterHeight = 1.8f;
        private bool subscribed;
        private bool pendingRecoveryRebuild;

        public Transform Locoball => locoball;
        public SphereCollider LocoballCollider => locoballCollider;
        public ApexHumanoidFootTether LeftFootTether => leftFootTether;
        public ApexHumanoidFootTether RightFootTether => rightFootTether;
        public float CharacterHeight => characterHeight;

        private void Awake()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            Configure(humanoid);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (!pendingRecoveryRebuild)
            {
                return;
            }

            pendingRecoveryRebuild = false;
            if (activeRagdoll == null || activeRagdoll.State != ApexRagdollState.Limp)
            {
                RebuildLocoball();
            }
        }

        public void Configure(ApexPhysicalHumanoid owner)
        {
            Unsubscribe();

            humanoid = owner;
            supportRig = owner != null ? owner.SupportRig : GetComponent<ApexHumanoidSupportRig>();
            if (supportRig == null)
            {
                supportRig = GetComponent<ApexHumanoidSupportRig>();
            }

            supportBody = supportRig != null ? supportRig.SupportBody : null;
            activeRagdoll = owner != null ? owner.ActiveRagdoll : null;

            ResolveHumanoidBones();
            RebuildLocoball();
            Subscribe();
        }

        public void RebuildLocoball()
        {
            if (!ResolveRequiredReferences())
            {
                return;
            }

            characterHeight = supportRig != null
                ? Mathf.Max(0.5f, supportRig.CharacterHeight)
                : CalculateCharacterHeight();

            Quaternion uprightRotation = GetUprightRotation();
            float groundHeight = ResolveGroundHeight(physicalHips.position);
            float radius = Mathf.Max(0.04f, characterHeight * radiusRatio);
            float clearance = characterHeight * floorClearanceRatio;

            supportBody.transform.SetPositionAndRotation(physicalHips.position, uprightRotation);
            supportBody.velocity = Vector3.zero;
            supportBody.angularVelocity = Vector3.zero;
            supportBody.centerOfMass = Vector3.down * characterHeight * 0.18f;

            ConfigureSupportGeometry(groundHeight, radius, clearance);
            ConfigureHipsAnchor();
            ConfigureLocoball(groundHeight, radius, clearance);
            ConfigureFootTethers(radius);
            ConfigureInternalCollisionFiltering();
            DelayRagdollImpacts();
        }

        private bool ResolveRequiredReferences()
        {
            if (humanoid == null)
            {
                humanoid = GetComponent<ApexPhysicalHumanoid>();
            }

            if (supportRig == null && humanoid != null)
            {
                supportRig = humanoid.SupportRig != null
                    ? humanoid.SupportRig
                    : GetComponent<ApexHumanoidSupportRig>();
            }

            if (supportBody == null && supportRig != null)
            {
                supportBody = supportRig.SupportBody;
            }

            if (activeRagdoll == null && humanoid != null)
            {
                activeRagdoll = humanoid.ActiveRagdoll;
            }

            ResolveHumanoidBones();
            return humanoid != null && supportRig != null && supportBody != null &&
                   physicalHips != null && physicalAnimator != null;
        }

        private void ResolveHumanoidBones()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            physicalAnimator = humanoid.PhysicalCharacter.GetComponentInChildren<Animator>(true);
            physicalHips = humanoid.PhysicalHips != null
                ? humanoid.PhysicalHips.transform
                : null;

            if (physicalAnimator != null && physicalAnimator.isHuman)
            {
                leftFoot = physicalAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = physicalAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        private void ConfigureSupportGeometry(float groundHeight, float radius, float clearance)
        {
            BoxCollider torso = supportBody.GetComponent<BoxCollider>();
            CapsuleCollider lowerBody = supportBody.GetComponent<CapsuleCollider>();
            SphereCollider oldGroundSphere = supportBody.GetComponent<SphereCollider>();

            Vector3 up = supportBody.transform.up;
            Transform chest = null;
            if (physicalAnimator != null && physicalAnimator.isHuman)
            {
                chest = physicalAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chest == null)
                {
                    chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Chest);
                }
                if (chest == null)
                {
                    chest = physicalAnimator.GetBoneTransform(HumanBodyBones.Spine);
                }
            }

            if (torso != null)
            {
                Vector3 torsoWorldCenter = chest != null
                    ? Vector3.Lerp(physicalHips.position, chest.position, 0.7f)
                    : physicalHips.position + up * characterHeight * 0.15f;
                torso.center = supportBody.transform.InverseTransformPoint(torsoWorldCenter);
                torso.size = new Vector3(
                    characterHeight * 0.30f,
                    characterHeight * 0.28f,
                    characterHeight * 0.18f);
                torso.enabled = true;
            }

            if (lowerBody != null)
            {
                float lowerTop = physicalHips.position.y + characterHeight * 0.04f;
                float lowerBottom = groundHeight + radius * 1.15f + clearance;
                float height = Mathf.Max(radius * 2f, lowerTop - lowerBottom);
                Vector3 worldCenter = new Vector3(
                    physicalHips.position.x,
                    (lowerTop + lowerBottom) * 0.5f,
                    physicalHips.position.z);

                lowerBody.direction = 1;
                lowerBody.radius = radius * 0.92f;
                lowerBody.height = Mathf.Max(height, lowerBody.radius * 2f);
                lowerBody.center = supportBody.transform.InverseTransformPoint(worldCenter);
                lowerBody.enabled = true;
            }

            // Replaced by the child locoball so floor contact has an explicit transform.
            if (oldGroundSphere != null)
            {
                oldGroundSphere.enabled = false;
            }
        }

        private void ConfigureHipsAnchor()
        {
            if (humanoid == null || humanoid.PhysicalHips == null || supportBody == null)
            {
                return;
            }

            ConfigurableJoint hipsJoint = humanoid.PhysicalHips.Joint;
            if (hipsJoint != null)
            {
                hipsJoint.connectedBody = supportBody;
                hipsJoint.autoConfigureConnectedAnchor = false;
                hipsJoint.anchor = Vector3.zero;
                hipsJoint.connectedAnchor = supportBody.transform.InverseTransformPoint(physicalHips.position);
                hipsJoint.xMotion = ConfigurableJointMotion.Locked;
                hipsJoint.yMotion = ConfigurableJointMotion.Locked;
                hipsJoint.zMotion = ConfigurableJointMotion.Locked;
                hipsJoint.angularXMotion = ConfigurableJointMotion.Locked;
                hipsJoint.angularYMotion = ConfigurableJointMotion.Locked;
                hipsJoint.angularZMotion = ConfigurableJointMotion.Locked;
                hipsJoint.enableCollision = false;
            }

            Transform anchor = supportRig.HipsAnchor;
            if (anchor != null)
            {
                anchor.localPosition = supportBody.transform.InverseTransformPoint(physicalHips.position);
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;

                if (humanoid.TargetRootDriver != null && humanoid.TargetHips != null)
                {
                    humanoid.TargetRootDriver.Configure(
                        anchor,
                        humanoid.TargetHips,
                        true,
                        true,
                        true);
                }
            }
        }

        private void ConfigureLocoball(float groundHeight, float radius, float clearance)
        {
            Transform existing = supportBody.transform.Find("Apex Humanoid Locoball");
            if (existing == null)
            {
                GameObject locoballObject = new GameObject("Apex Humanoid Locoball");
                existing = locoballObject.transform;
                existing.SetParent(supportBody.transform, false);
            }

            locoball = existing;
            locoball.localScale = Vector3.one;
            locoball.position = new Vector3(
                physicalHips.position.x,
                groundHeight + radius + clearance,
                physicalHips.position.z);
            locoball.rotation = supportBody.rotation;

            locoballCollider = GetOrAdd<SphereCollider>(locoball.gameObject);
            locoballCollider.center = Vector3.zero;
            locoballCollider.radius = radius;
            locoballCollider.enabled = true;

            leftFootAnchor = GetOrCreateChild(locoball, "Left Foot Anchor");
            rightFootAnchor = GetOrCreateChild(locoball, "Right Foot Anchor");

            float spread = characterHeight * footSpreadRatio;
            leftFootAnchor.localPosition = Vector3.left * spread;
            rightFootAnchor.localPosition = Vector3.right * spread;
            leftFootAnchor.localRotation = Quaternion.identity;
            rightFootAnchor.localRotation = Quaternion.identity;
        }

        private void ConfigureFootTethers(float radius)
        {
            float tetherDistance = Mathf.Max(radius * 0.5f, characterHeight * tetherDistanceRatio);
            float limpSlack = Mathf.Max(tetherDistance, characterHeight * limpSlackRatio);

            if (leftFoot != null)
            {
                leftFootTether = GetOrAdd<ApexHumanoidFootTether>(leftFoot.gameObject);
                leftFootTether.Configure(
                    supportBody,
                    leftFootAnchor,
                    activeRagdoll,
                    tetherSpring,
                    tetherDamper,
                    tetherDistance,
                    limpSlack);
            }

            if (rightFoot != null)
            {
                rightFootTether = GetOrAdd<ApexHumanoidFootTether>(rightFoot.gameObject);
                rightFootTether.Configure(
                    supportBody,
                    rightFootAnchor,
                    activeRagdoll,
                    tetherSpring,
                    tetherDamper,
                    tetherDistance,
                    limpSlack);
            }
        }

        private void ConfigureInternalCollisionFiltering()
        {
            if (locoballCollider == null || humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            Collider[] physicalColliders =
                humanoid.PhysicalCharacter.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < physicalColliders.Length; i++)
            {
                Collider physicalCollider = physicalColliders[i];
                if (physicalCollider != null && physicalCollider != locoballCollider)
                {
                    Physics.IgnoreCollision(locoballCollider, physicalCollider, true);
                }
            }
        }

        private void DelayRagdollImpacts()
        {
            if (humanoid == null || humanoid.PhysicalCharacter == null)
            {
                return;
            }

            ApexRagdollBone[] bones =
                humanoid.PhysicalCharacter.GetComponentsInChildren<ApexRagdollBone>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i]?.DelayImpacts(impactArmingDelay);
            }
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
            if (state == ApexRagdollState.Recovering || state == ApexRagdollState.Active)
            {
                pendingRecoveryRebuild = true;
            }
        }

        private Quaternion GetUprightRotation()
        {
            Vector3 forward = physicalHips != null
                ? Vector3.ProjectOnPlane(physicalHips.forward, Vector3.up)
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

        private float ResolveGroundHeight(Vector3 hipsPosition)
        {
            Vector3 origin = hipsPosition + Vector3.up * characterHeight;
            float distance = characterHeight * 3f;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            float height = 0f;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || IsInternalCollider(hit.collider))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    height = hit.point.y;
                    found = true;
                }
            }

            if (found)
            {
                return height;
            }

            float soleHeight = float.PositiveInfinity;
            IncludeFootSole(leftFoot, ref soleHeight);
            IncludeFootSole(rightFoot, ref soleHeight);
            if (!float.IsPositiveInfinity(soleHeight))
            {
                return soleHeight;
            }

            return hipsPosition.y - characterHeight * 0.55f;
        }

        private bool IsInternalCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            return humanoid != null && candidate.transform.IsChildOf(humanoid.transform);
        }

        private void IncludeFootSole(Transform foot, ref float soleHeight)
        {
            if (foot == null)
            {
                return;
            }

            Collider footCollider = foot.GetComponent<Collider>();
            float candidate = footCollider != null
                ? footCollider.bounds.min.y
                : foot.position.y - characterHeight * 0.035f;
            soleHeight = Mathf.Min(soleHeight, candidate);
        }

        private float CalculateCharacterHeight()
        {
            if (physicalAnimator != null && physicalAnimator.isHuman)
            {
                Transform head = physicalAnimator.GetBoneTransform(HumanBodyBones.Head);
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

            return 1.8f;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            radiusRatio = Mathf.Max(0.01f, radiusRatio);
            floorClearanceRatio = Mathf.Max(0f, floorClearanceRatio);
            footSpreadRatio = Mathf.Max(0f, footSpreadRatio);
            tetherSpring = Mathf.Max(0f, tetherSpring);
            tetherDamper = Mathf.Max(0f, tetherDamper);
            tetherDistanceRatio = Mathf.Max(0f, tetherDistanceRatio);
            limpSlackRatio = Mathf.Max(tetherDistanceRatio, limpSlackRatio);
            impactArmingDelay = Mathf.Max(0f, impactArmingDelay);
        }

        private void OnDrawGizmosSelected()
        {
            if (locoball == null || locoballCollider == null)
            {
                return;
            }

            Gizmos.matrix = locoball.localToWorldMatrix;
            Gizmos.DrawWireSphere(locoballCollider.center, locoballCollider.radius);
            Gizmos.matrix = Matrix4x4.identity;

            if (leftFootAnchor != null && leftFoot != null)
            {
                Gizmos.DrawLine(leftFootAnchor.position, leftFoot.position);
            }
            if (rightFootAnchor != null && rightFoot != null)
            {
                Gizmos.DrawLine(rightFootAnchor.position, rightFoot.position);
            }
        }
#endif
    }
}

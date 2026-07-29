using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Selects how an automatically converted humanoid is configured.
    /// PhysicalPlayer is retained for old serialized content; the 0.3.x authoring
    /// workflow is PC Physical NPC only until the desktop character system is stable.
    /// </summary>
    public enum ApexPhysicalHumanoidMode
    {
        ActiveRagdoll = 0,
        PhysicalNPC = 1,
        PhysicalPlayer = 2
    }

    /// <summary>
    /// Stores the generated pieces of an Apex physical humanoid conversion.
    /// The animated target drives a visible physical clone. Physical NPCs are now
    /// coordinated through one ApexPCPhysicalCharacter component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexPhysicalHumanoid : MonoBehaviour
    {
        [SerializeField] private ApexPhysicalHumanoidMode mode;

        [Header("Generated Hierarchies")]
        [SerializeField] private GameObject animatedTargetCharacter;
        [SerializeField] private GameObject physicalCharacter;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform targetHips;
        [SerializeField] private ApexRagdollBone physicalHips;

        [Header("PC Character System")]
        [SerializeField] private ApexPCPhysicalCharacter pcPhysicalCharacter;
        [SerializeField] private ApexPCProceduralGait pcProceduralGait;

        [Header("Legacy Apex Systems")]
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private ApexHumanoidTargetRootDriver targetRootDriver;
        [SerializeField] private ApexHumanoidTrackingDriver trackingDriver;
        [SerializeField] private ApexRagdollCollisionFilter collisionFilter;
        [SerializeField] private ApexHumanoidSupportRig supportRig;
        [SerializeField] private ApexHumanoidPhysicalController physicalController;
        [SerializeField] private ApexHumanoidLocoballRig locoballRig;
        [SerializeField] private ApexHumanoidTorsoHarness torsoHarness;

        [Header("Legacy Player and NPC Systems")]
        [SerializeField] private ApexHumanoidPlayerMotor humanoidPlayerMotor;
        [SerializeField] private ApexPhysicalPlayerRig legacyPlayerRig;
        [SerializeField] private ApexNPCNavigator npcNavigator;
        [SerializeField] private ApexNPCMotor npcMotor;
        [SerializeField] private ApexNPCBrain npcBrain;

        public ApexPhysicalHumanoidMode Mode => mode;
        public GameObject AnimatedTargetCharacter => animatedTargetCharacter;
        public GameObject PhysicalCharacter => physicalCharacter;
        public Animator TargetAnimator => targetAnimator;
        public Transform TargetHips => targetHips;
        public ApexRagdollBone PhysicalHips => physicalHips;
        public ApexPCPhysicalCharacter PCPhysicalCharacter => pcPhysicalCharacter;
        public ApexPCProceduralGait PCProceduralGait => pcProceduralGait;
        public ApexActiveRagdoll ActiveRagdoll => activeRagdoll;
        public ApexHumanoidTargetRootDriver TargetRootDriver => targetRootDriver;
        public ApexHumanoidTrackingDriver TrackingDriver => trackingDriver;
        public ApexRagdollCollisionFilter CollisionFilter => collisionFilter;
        public ApexHumanoidSupportRig SupportRig => supportRig;
        public ApexHumanoidPhysicalController PhysicalController => physicalController;
        public ApexHumanoidLocoballRig LocoballRig => locoballRig;
        public ApexHumanoidTorsoHarness TorsoHarness => torsoHarness;
        public ApexHumanoidPlayerMotor HumanoidPlayerMotor => humanoidPlayerMotor;
        public ApexPhysicalPlayerRig LegacyPlayerRig => legacyPlayerRig;
        public ApexNPCNavigator NPCNavigator => npcNavigator;
        public ApexNPCMotor NPCMotor => npcMotor;
        public ApexNPCBrain NPCBrain => npcBrain;

        private void Awake()
        {
            if (mode == ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                EnsurePCPhysicalCharacter();
            }
        }

        public void Configure(
            ApexPhysicalHumanoidMode newMode,
            GameObject newAnimatedTargetCharacter,
            GameObject newPhysicalCharacter,
            Animator newTargetAnimator,
            Transform newTargetHips,
            ApexRagdollBone newPhysicalHips,
            ApexActiveRagdoll newActiveRagdoll,
            ApexHumanoidTargetRootDriver newTargetRootDriver,
            ApexHumanoidTrackingDriver newTrackingDriver,
            ApexRagdollCollisionFilter newCollisionFilter,
            ApexHumanoidPlayerMotor newHumanoidPlayerMotor,
            ApexNPCNavigator newNpcNavigator,
            ApexNPCMotor newNpcMotor,
            ApexNPCBrain newNpcBrain)
        {
            mode = newMode;
            animatedTargetCharacter = newAnimatedTargetCharacter;
            physicalCharacter = newPhysicalCharacter;
            targetAnimator = newTargetAnimator;
            targetHips = newTargetHips;
            physicalHips = newPhysicalHips;
            activeRagdoll = newActiveRagdoll;
            targetRootDriver = newTargetRootDriver;
            trackingDriver = newTrackingDriver;
            collisionFilter = newCollisionFilter;
            humanoidPlayerMotor = newHumanoidPlayerMotor;
            legacyPlayerRig = null;
            npcNavigator = newNpcNavigator;
            npcMotor = newNpcMotor;
            npcBrain = newNpcBrain;

            if (mode == ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                EnsurePCPhysicalCharacter();
            }
        }

        public ApexPCPhysicalCharacter EnsurePCPhysicalCharacter()
        {
            if (mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                return pcPhysicalCharacter;
            }

            if (pcPhysicalCharacter == null)
            {
                pcPhysicalCharacter = GetComponent<ApexPCPhysicalCharacter>();
            }

            if (pcPhysicalCharacter == null)
            {
                pcPhysicalCharacter = gameObject.AddComponent<ApexPCPhysicalCharacter>();
            }

            pcPhysicalCharacter.Configure(this);

            if (pcProceduralGait == null)
            {
                pcProceduralGait = GetComponent<ApexPCProceduralGait>();
            }

            if (pcProceduralGait == null)
            {
                pcProceduralGait = gameObject.AddComponent<ApexPCProceduralGait>();
            }

            pcProceduralGait.Configure(pcPhysicalCharacter);
            return pcPhysicalCharacter;
        }

        /// <summary>
        /// Legacy 0.1/0.2 support component retained so older prefabs can deserialize.
        /// New 0.3.x PC characters do not use it for active locomotion.
        /// </summary>
        public ApexHumanoidSupportRig EnsureSupportRig()
        {
            if (mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                return supportRig;
            }

            if (supportRig == null)
            {
                supportRig = GetComponent<ApexHumanoidSupportRig>();
            }

            if (supportRig == null)
            {
                supportRig = gameObject.AddComponent<ApexHumanoidSupportRig>();
            }

            supportRig.Configure(this);
            return supportRig;
        }

        /// <summary>
        /// Legacy 0.2 controller retained for serialized compatibility.
        /// </summary>
        public ApexHumanoidPhysicalController EnsurePhysicalController()
        {
            if (mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                return physicalController;
            }

            if (supportRig == null)
            {
                EnsureSupportRig();
            }

            if (physicalController == null)
            {
                physicalController = GetComponent<ApexHumanoidPhysicalController>();
            }

            if (physicalController == null)
            {
                physicalController = gameObject.AddComponent<ApexHumanoidPhysicalController>();
            }

            physicalController.Configure(this);
            return physicalController;
        }

        public ApexHumanoidLocoballRig EnsureLocoballRig()
        {
            if (locoballRig == null)
            {
                locoballRig = GetComponent<ApexHumanoidLocoballRig>();
            }

            return locoballRig;
        }

        public ApexHumanoidTorsoHarness EnsureTorsoHarness()
        {
            if (torsoHarness == null)
            {
                torsoHarness = GetComponent<ApexHumanoidTorsoHarness>();
            }

            return torsoHarness;
        }

        public void TunePhysicalNpcMuscles()
        {
            if (mode != ApexPhysicalHumanoidMode.PhysicalNPC || physicalCharacter == null)
            {
                return;
            }

            Animator animator = physicalCharacter.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            SetBoneMuscle(animator, HumanBodyBones.Spine, 1.65f);
            SetBoneMuscle(animator, HumanBodyBones.Chest, 1.65f);
            SetBoneMuscle(animator, HumanBodyBones.UpperChest, 1.65f);
            SetBoneMuscle(animator, HumanBodyBones.Neck, 1.15f);
            SetBoneMuscle(animator, HumanBodyBones.Head, 1.05f);
            SetBoneMuscle(animator, HumanBodyBones.LeftUpperLeg, 1.45f);
            SetBoneMuscle(animator, HumanBodyBones.RightUpperLeg, 1.45f);
            SetBoneMuscle(animator, HumanBodyBones.LeftLowerLeg, 1.35f);
            SetBoneMuscle(animator, HumanBodyBones.RightLowerLeg, 1.35f);
            SetBoneMuscle(animator, HumanBodyBones.LeftFoot, 1.1f);
            SetBoneMuscle(animator, HumanBodyBones.RightFoot, 1.1f);
        }

        private static void SetBoneMuscle(Animator animator, HumanBodyBones role, float multiplier)
        {
            Transform bone = animator.GetBoneTransform(role);
            if (bone == null)
            {
                return;
            }

            ApexRagdollBone ragdollBone = bone.GetComponent<ApexRagdollBone>();
            ragdollBone?.SetMuscleMultiplier(multiplier);
        }
    }
}

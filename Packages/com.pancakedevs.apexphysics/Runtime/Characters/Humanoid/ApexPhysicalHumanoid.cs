using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Stores the generated pieces of an Apex physical humanoid conversion.
    /// The animated target drives a visible physical clone through ApexActiveRagdoll.
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

        [Header("Apex Systems")]
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private ApexHumanoidTargetRootDriver targetRootDriver;
        [SerializeField] private ApexHumanoidTrackingDriver trackingDriver;
        [SerializeField] private ApexRagdollCollisionFilter collisionFilter;
        [SerializeField] private ApexHumanoidSupportRig supportRig;
        [SerializeField] private ApexHumanoidLocoballRig locoballRig;
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
        public ApexActiveRagdoll ActiveRagdoll => activeRagdoll;
        public ApexHumanoidTargetRootDriver TargetRootDriver => targetRootDriver;
        public ApexHumanoidTrackingDriver TrackingDriver => trackingDriver;
        public ApexRagdollCollisionFilter CollisionFilter => collisionFilter;
        public ApexHumanoidSupportRig SupportRig => supportRig;
        public ApexHumanoidLocoballRig LocoballRig => locoballRig;
        public ApexHumanoidPlayerMotor HumanoidPlayerMotor => humanoidPlayerMotor;
        public ApexPhysicalPlayerRig LegacyPlayerRig => legacyPlayerRig;
        public ApexNPCNavigator NPCNavigator => npcNavigator;
        public ApexNPCMotor NPCMotor => npcMotor;
        public ApexNPCBrain NPCBrain => npcBrain;

        private void Awake()
        {
            if (mode == ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                EnsureSupportRig();
                EnsureLocoballRig();
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
                EnsureSupportRig();
                EnsureLocoballRig();
            }
        }

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

        public ApexHumanoidLocoballRig EnsureLocoballRig()
        {
            if (mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                return locoballRig;
            }

            if (supportRig == null)
            {
                EnsureSupportRig();
            }

            if (locoballRig == null)
            {
                locoballRig = GetComponent<ApexHumanoidLocoballRig>();
            }

            if (locoballRig == null)
            {
                locoballRig = gameObject.AddComponent<ApexHumanoidLocoballRig>();
            }

            locoballRig.Configure(this);
            return locoballRig;
        }
    }
}

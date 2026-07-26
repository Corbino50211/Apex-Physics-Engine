using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Selects how an automatically converted humanoid is configured.
    /// Kept beside ApexPhysicalHumanoid so Unity cannot partially import the component
    /// while omitting its required mode type from a Git package refresh.
    /// </summary>
    public enum ApexPhysicalHumanoidMode
    {
        ActiveRagdoll = 0,
        PhysicalNPC = 1,
        PhysicalPlayer = 2
    }

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
        [SerializeField] private ApexHumanoidPhysicalController physicalController;

        [Header("Legacy Support Add-ons")]
        [SerializeField] private ApexHumanoidLocoballRig locoballRig;
        [SerializeField] private ApexHumanoidTorsoHarness torsoHarness;

        [Header("Player and NPC Systems")]
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
                EnsureSupportRig();
                EnsurePhysicalController();
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
                EnsurePhysicalController();
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

            DisableLegacySupportAddons();
            physicalController.Configure(this);
            return physicalController;
        }

        /// <summary>
        /// Retained only so older serialized prefabs can still deserialize. New physical
        /// NPCs do not install or use the locoball foot-tether system.
        /// </summary>
        public ApexHumanoidLocoballRig EnsureLocoballRig()
        {
            if (locoballRig == null)
            {
                locoballRig = GetComponent<ApexHumanoidLocoballRig>();
            }

            return locoballRig;
        }

        /// <summary>
        /// Retained only so older serialized prefabs can still deserialize. New physical
        /// NPCs use target-driven spine muscles instead of a separate chest spring.
        /// </summary>
        public ApexHumanoidTorsoHarness EnsureTorsoHarness()
        {
            if (torsoHarness == null)
            {
                torsoHarness = GetComponent<ApexHumanoidTorsoHarness>();
            }

            return torsoHarness;
        }

        private void DisableLegacySupportAddons()
        {
            locoballRig = GetComponent<ApexHumanoidLocoballRig>();
            if (locoballRig != null)
            {
                locoballRig.enabled = false;
            }

            torsoHarness = GetComponent<ApexHumanoidTorsoHarness>();
            if (torsoHarness != null)
            {
                torsoHarness.enabled = false;
            }

            ApexHumanoidFootTether[] tethers = GetComponentsInChildren<ApexHumanoidFootTether>(true);
            for (int i = 0; i < tethers.Length; i++)
            {
                tethers[i]?.SetTetherActive(false);
            }
        }
    }
}

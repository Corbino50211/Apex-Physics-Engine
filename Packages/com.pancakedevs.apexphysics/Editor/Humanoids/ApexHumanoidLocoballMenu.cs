using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexHumanoidLocoballMenu
    {
        private const string MenuPath =
            "Apex Physics Engine/Characters/Rebuild Selected NPC Physical Controller";
        private const string LegacyMenuPath =
            "Apex Physics Engine/Characters/Rebuild Selected NPC Support Rig";

        [MenuItem(MenuPath, false, 235)]
        private static void RebuildSelectedNpcPhysicalController()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Apex NPC Physical Controller",
                    "Exit Play Mode before rebuilding the physical controller.",
                    "OK");
                return;
            }

            GameObject selected = Selection.activeGameObject;
            ApexPhysicalHumanoid humanoid = selected != null
                ? selected.GetComponentInParent<ApexPhysicalHumanoid>()
                : null;

            if (humanoid == null || humanoid.Mode != ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                EditorUtility.DisplayDialog(
                    "Apex NPC Physical Controller",
                    "Select a generated Apex Physical NPC or one of its children.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Apex NPC Physical Controller");

            RemoveLegacySupportAddons(humanoid);

            ApexHumanoidSupportRig supportRig = humanoid.SupportRig != null
                ? humanoid.SupportRig
                : humanoid.GetComponent<ApexHumanoidSupportRig>();
            if (supportRig == null)
            {
                supportRig = Undo.AddComponent<ApexHumanoidSupportRig>(humanoid.gameObject);
            }

            Undo.RecordObject(supportRig, "Rebuild Apex NPC Locomotion Body");
            supportRig.Configure(humanoid);
            supportRig.RebuildSupport();
            supportRig.SnapSupportToHumanoid();

            ApexHumanoidPhysicalController controller = humanoid.PhysicalController != null
                ? humanoid.PhysicalController
                : humanoid.GetComponent<ApexHumanoidPhysicalController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<ApexHumanoidPhysicalController>(humanoid.gameObject);
            }

            Undo.RecordObject(controller, "Configure Apex NPC Physical Controller");
            controller.Configure(humanoid);

            if (humanoid.ActiveRagdoll != null)
            {
                Undo.RecordObject(humanoid.ActiveRagdoll, "Refresh Apex Active Ragdoll");
                humanoid.ActiveRagdoll.RefreshBones();
                humanoid.ActiveRagdoll.CaptureCurrentPose();
                EditorUtility.SetDirty(humanoid.ActiveRagdoll);
            }

            EditorUtility.SetDirty(humanoid);
            EditorUtility.SetDirty(supportRig);
            EditorUtility.SetDirty(controller);

            if (humanoid.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(humanoid.gameObject.scene);
            }

            Selection.activeGameObject = humanoid.gameObject;
            EditorGUIUtility.PingObject(humanoid.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(LegacyMenuPath, false, 236)]
        private static void RebuildSelectedNpcPhysicalControllerLegacyAlias()
        {
            RebuildSelectedNpcPhysicalController();
        }

        private static void RemoveLegacySupportAddons(ApexPhysicalHumanoid humanoid)
        {
            ApexHumanoidFootTether[] tethers =
                humanoid.GetComponentsInChildren<ApexHumanoidFootTether>(true);
            for (int i = 0; i < tethers.Length; i++)
            {
                ApexHumanoidFootTether tether = tethers[i];
                if (tether == null)
                {
                    continue;
                }

                SpringJoint joint = tether.Joint;
                if (joint != null)
                {
                    Undo.DestroyObjectImmediate(joint);
                }

                Undo.DestroyObjectImmediate(tether);
            }

            ApexHumanoidLocoballRig locoballRig =
                humanoid.GetComponent<ApexHumanoidLocoballRig>();
            if (locoballRig != null)
            {
                Undo.DestroyObjectImmediate(locoballRig);
            }

            ApexHumanoidTorsoHarness torsoHarness =
                humanoid.GetComponent<ApexHumanoidTorsoHarness>();
            if (torsoHarness != null)
            {
                Undo.DestroyObjectImmediate(torsoHarness);
            }

            ApexHumanoidSupportRig supportRig = humanoid.SupportRig != null
                ? humanoid.SupportRig
                : humanoid.GetComponent<ApexHumanoidSupportRig>();
            if (supportRig == null || supportRig.SupportBody == null)
            {
                return;
            }

            Transform legacyLocoball =
                supportRig.SupportBody.transform.Find("Apex Humanoid Locoball");
            if (legacyLocoball != null)
            {
                Undo.DestroyObjectImmediate(legacyLocoball.gameObject);
            }

            Transform legacyChestAnchor =
                supportRig.SupportBody.transform.Find("Chest Harness Anchor");
            if (legacyChestAnchor != null)
            {
                Undo.DestroyObjectImmediate(legacyChestAnchor.gameObject);
            }
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(LegacyMenuPath, true)]
        private static bool ValidateRebuildSelectedNpcPhysicalController()
        {
            GameObject selected = Selection.activeGameObject;
            ApexPhysicalHumanoid humanoid = selected != null
                ? selected.GetComponentInParent<ApexPhysicalHumanoid>()
                : null;
            return humanoid != null && humanoid.Mode == ApexPhysicalHumanoidMode.PhysicalNPC;
        }
    }
}

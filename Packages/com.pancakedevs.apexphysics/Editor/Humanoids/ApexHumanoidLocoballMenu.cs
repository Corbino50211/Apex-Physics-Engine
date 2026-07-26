using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexHumanoidLocoballMenu
    {
        private const string MenuPath =
            "Apex Physics Engine/Characters/Rebuild Selected NPC Support Rig";

        [MenuItem(MenuPath, false, 235)]
        private static void RebuildSelectedNpcSupportRig()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Apex NPC Support Rig",
                    "Exit Play Mode before rebuilding the locoball, torso harness, and standing pose.",
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
                    "Apex NPC Support Rig",
                    "Select a generated Apex Physical NPC or one of its children.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Apex NPC Support Rig");

            ApexHumanoidSupportRig supportRig = humanoid.SupportRig != null
                ? humanoid.SupportRig
                : humanoid.GetComponent<ApexHumanoidSupportRig>();
            if (supportRig == null)
            {
                supportRig = Undo.AddComponent<ApexHumanoidSupportRig>(humanoid.gameObject);
            }

            supportRig.Configure(humanoid);

            ApexHumanoidLocoballRig locoballRig = humanoid.LocoballRig != null
                ? humanoid.LocoballRig
                : humanoid.GetComponent<ApexHumanoidLocoballRig>();
            if (locoballRig == null)
            {
                locoballRig = Undo.AddComponent<ApexHumanoidLocoballRig>(humanoid.gameObject);
            }

            ApexHumanoidTorsoHarness torsoHarness = humanoid.TorsoHarness != null
                ? humanoid.TorsoHarness
                : humanoid.GetComponent<ApexHumanoidTorsoHarness>();
            if (torsoHarness == null)
            {
                torsoHarness = Undo.AddComponent<ApexHumanoidTorsoHarness>(humanoid.gameObject);
            }

            Undo.RecordObject(humanoid, "Assign Apex NPC Support Rig");
            Undo.RecordObject(supportRig, "Rebuild Apex NPC Support");
            Undo.RecordObject(locoballRig, "Rebuild Apex NPC Locoball");
            Undo.RecordObject(torsoHarness, "Rebuild Apex NPC Torso Harness");

            locoballRig.Configure(humanoid);
            locoballRig.RebuildLocoball();
            torsoHarness.Configure(humanoid);
            torsoHarness.RebuildHarness();

            humanoid.ActiveRagdoll?.RefreshBones();
            humanoid.ActiveRagdoll?.CaptureCurrentPose();

            EditorUtility.SetDirty(humanoid);
            EditorUtility.SetDirty(supportRig);
            EditorUtility.SetDirty(locoballRig);
            EditorUtility.SetDirty(torsoHarness);
            if (humanoid.ActiveRagdoll != null)
            {
                EditorUtility.SetDirty(humanoid.ActiveRagdoll);
            }

            if (humanoid.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(humanoid.gameObject.scene);
            }

            Selection.activeGameObject = humanoid.gameObject;
            EditorGUIUtility.PingObject(humanoid.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRebuildSelectedNpcSupportRig()
        {
            GameObject selected = Selection.activeGameObject;
            ApexPhysicalHumanoid humanoid = selected != null
                ? selected.GetComponentInParent<ApexPhysicalHumanoid>()
                : null;
            return humanoid != null && humanoid.Mode == ApexPhysicalHumanoidMode.PhysicalNPC;
        }
    }
}

using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexHumanoidLocoballMenu
    {
        private const string MenuPath =
            "Apex Physics Engine/Characters/Rebuild Selected PC Physical Character";

        [MenuItem(MenuPath, false, 235)]
        private static void RebuildSelectedPcPhysicalCharacter()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Apex PC Physical Character",
                    "Exit Play Mode before rebuilding the PC physical character.",
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
                    "Apex PC Physical Character",
                    "Select a generated Apex Physical NPC or one of its children.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Apex PC Physical Character");

            RemoveLegacySupportAddons(humanoid);

            ApexPCPhysicalCharacter character = humanoid.PCPhysicalCharacter != null
                ? humanoid.PCPhysicalCharacter
                : humanoid.GetComponent<ApexPCPhysicalCharacter>();
            if (character == null)
            {
                character = Undo.AddComponent<ApexPCPhysicalCharacter>(humanoid.gameObject);
            }

            Undo.RecordObject(character, "Configure Apex PC Physical Character");
            character.Configure(humanoid);
            character.Rebuild();

            ApexPCMotorFitter fitter = humanoid.GetComponent<ApexPCMotorFitter>();
            if (fitter == null)
            {
                fitter = Undo.AddComponent<ApexPCMotorFitter>(humanoid.gameObject);
            }

            Undo.RecordObject(fitter, "Fit Apex PC Character Motor");
            fitter.Configure(humanoid);
            fitter.FitNow();

            EditorUtility.SetDirty(humanoid);
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(fitter);

            if (humanoid.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(humanoid.gameObject.scene);
            }

            Selection.activeGameObject = humanoid.gameObject;
            EditorGUIUtility.PingObject(humanoid.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
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

            DestroyIfPresent<ApexHumanoidLocoballRig>(humanoid.gameObject);
            DestroyIfPresent<ApexHumanoidTorsoHarness>(humanoid.gameObject);
            DestroyIfPresent<ApexHumanoidPhysicalController>(humanoid.gameObject);

            ApexHumanoidSupportRig supportRig = humanoid.SupportRig != null
                ? humanoid.SupportRig
                : humanoid.GetComponent<ApexHumanoidSupportRig>();
            if (supportRig != null)
            {
                Rigidbody supportBody = supportRig.SupportBody;
                Undo.DestroyObjectImmediate(supportRig);
                if (supportBody != null)
                {
                    Undo.DestroyObjectImmediate(supportBody.gameObject);
                }
            }
        }

        private static void DestroyIfPresent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRebuildSelectedPcPhysicalCharacter()
        {
            GameObject selected = Selection.activeGameObject;
            ApexPhysicalHumanoid humanoid = selected != null
                ? selected.GetComponentInParent<ApexPhysicalHumanoid>()
                : null;
            return humanoid != null && humanoid.Mode == ApexPhysicalHumanoidMode.PhysicalNPC;
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexClimbingMenu
    {
        private const string MakeClimbablePath =
            "Apex Physics Engine/Player/Make Selected Object Climbable";
        private const string AddClimberPath =
            "Apex Physics Engine/Player/Add Climbing to Selected OpenXR Rig";

        [MenuItem(MakeClimbablePath, false, 140)]
        private static void MakeSelectedClimbable()
        {
            GameObject selected = Selection.activeGameObject;
            if (!IsEditableSceneObject(selected))
            {
                EditorUtility.DisplayDialog(
                    "Scene Object Required",
                    "Select a collider object in the Hierarchy, not an imported asset in the Project window.",
                    "OK");
                return;
            }

            if (selected.GetComponentInChildren<Collider>(true) == null)
            {
                EditorUtility.DisplayDialog(
                    "Collider Required",
                    "Add a Collider to the selected object or one of its children first.",
                    "OK");
                return;
            }

            ApexClimbable climbable = selected.GetComponent<ApexClimbable>();
            if (climbable == null)
            {
                climbable = Undo.AddComponent<ApexClimbable>(selected);
            }

            EditorUtility.SetDirty(climbable);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = selected;
            Debug.Log("Made the selected object climbable for Apex physical VR hands.", selected);
        }

        [MenuItem(MakeClimbablePath, true)]
        private static bool ValidateMakeSelectedClimbable()
        {
            return IsEditableSceneObject(Selection.activeGameObject);
        }

        [MenuItem(AddClimberPath, false, 141)]
        private static void AddClimbingToSelectedRig()
        {
            GameObject selected = Selection.activeGameObject;
            if (!IsEditableSceneObject(selected))
            {
                return;
            }

            ApexPhysicalOpenXRBody body = selected.GetComponentInParent<ApexPhysicalOpenXRBody>();
            if (body == null)
            {
                body = selected.GetComponentInChildren<ApexPhysicalOpenXRBody>(true);
            }

            if (body == null)
            {
                EditorUtility.DisplayDialog(
                    "OpenXR Rig Required",
                    "Select the Apex Physical OpenXR Player or one of its children.",
                    "OK");
                return;
            }

            ApexPhysicalVRClimber climber = body.GetComponent<ApexPhysicalVRClimber>();
            if (climber == null)
            {
                climber = Undo.AddComponent<ApexPhysicalVRClimber>(body.gameObject);
            }

            EditorUtility.SetDirty(climber);
            EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
            Selection.activeGameObject = body.gameObject;
            Debug.Log("Added physical climbing to the selected Apex OpenXR rig.", body.gameObject);
        }

        [MenuItem(AddClimberPath, true)]
        private static bool ValidateAddClimbingToSelectedRig()
        {
            GameObject selected = Selection.activeGameObject;
            return IsEditableSceneObject(selected) &&
                   (selected.GetComponentInParent<ApexPhysicalOpenXRBody>() != null ||
                    selected.GetComponentInChildren<ApexPhysicalOpenXRBody>(true) != null);
        }

        private static bool IsEditableSceneObject(GameObject gameObject)
        {
            return gameObject != null &&
                   gameObject.scene.IsValid() &&
                   gameObject.scene.isLoaded &&
                   !EditorUtility.IsPersistent(gameObject);
        }
    }
}

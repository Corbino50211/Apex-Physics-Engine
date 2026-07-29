using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexSafeHumanoidBinder
    {
        private const string BindPath =
            "Apex Physics Engine/Player/Bind Selected Humanoid Scene Instance";

        [MenuItem(BindPath, false, 122)]
        private static void BindSelectedHumanoidSceneInstance()
        {
            GameObject selected = Selection.activeGameObject;
            if (!IsValidSceneSelection(selected))
            {
                EditorUtility.DisplayDialog(
                    "Select a Scene Instance",
                    "Drag the Humanoid FBX from the Project window into the Hierarchy, then select that Hierarchy object. Imported FBX assets cannot be modified directly.",
                    "OK");
                return;
            }

            Animator animator = selected.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                EditorUtility.DisplayDialog(
                    "Valid Humanoid Required",
                    "The selected scene object needs a valid Humanoid Animator. Set the FBX Rig type to Humanoid and press Apply.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRBody physicalBody =
                Object.FindFirstObjectByType<ApexPhysicalOpenXRBody>(FindObjectsInactive.Include);
            if (physicalBody == null)
            {
                EditorUtility.DisplayDialog(
                    "OpenXR Rig Not Found",
                    "Create an Apex Physical OpenXR Rig first.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRHand[] hands = Object.FindObjectsByType<ApexPhysicalOpenXRHand>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            ApexPhysicalOpenXRHand leftHand = null;
            ApexPhysicalOpenXRHand rightHand = null;
            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                if (hand == null || !hand.transform.IsChildOf(physicalBody.transform))
                {
                    continue;
                }

                string lowerName = hand.name.ToLowerInvariant();
                if (lowerName.Contains("left"))
                {
                    leftHand = hand;
                }
                else if (lowerName.Contains("right"))
                {
                    rightHand = hand;
                }
            }

            if (leftHand == null || rightHand == null || physicalBody.Head == null)
            {
                EditorUtility.DisplayDialog(
                    "Rig References Missing",
                    "Apex could not find the tracked head and both physical hands under the OpenXR rig.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind Humanoid Scene Instance to Apex VR Rig");

            Undo.SetTransformParent(
                selected.transform,
                physicalBody.transform,
                "Parent Apex VR Avatar");
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;

            ApexPhysicalVRAvatar avatar = selected.GetComponent<ApexPhysicalVRAvatar>();
            if (avatar == null)
            {
                avatar = Undo.AddComponent<ApexPhysicalVRAvatar>(selected);
            }

            avatar.EditorConfigure(
                animator,
                physicalBody,
                physicalBody.Head,
                leftHand.transform,
                rightHand.transform);

            HideDebugVisuals(leftHand);
            HideDebugVisuals(rightHand);

            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = selected;
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("Bound Humanoid scene instance to the Apex Physical OpenXR rig.", selected);
        }

        [MenuItem(BindPath, true)]
        private static bool ValidateBindSelectedHumanoidSceneInstance()
        {
            return IsValidSceneSelection(Selection.activeGameObject);
        }

        private static bool IsValidSceneSelection(GameObject selected)
        {
            return selected != null &&
                   !EditorUtility.IsPersistent(selected) &&
                   selected.scene.IsValid() &&
                   selected.scene.isLoaded &&
                   !PrefabUtility.IsPartOfPrefabAsset(selected);
        }

        private static void HideDebugVisuals(ApexPhysicalOpenXRHand hand)
        {
            if (hand == null)
            {
                return;
            }

            Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                string lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("visual") || lowerName.Contains("sphere") || lowerName.Contains("debug"))
                {
                    Undo.RecordObject(renderer, "Hide Apex Debug Hand Visual");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }
    }
}

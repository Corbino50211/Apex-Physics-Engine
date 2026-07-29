using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexPhysicalVRAvatarMenu
    {
        private const string BindPath =
            "Apex Physics Engine/Player/Bind Selected Humanoid to Physical OpenXR Rig";

        [MenuItem(BindPath, false, 131)]
        private static void BindSelectedHumanoid()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "No Humanoid Selected",
                    "Select the Humanoid model root in the Hierarchy, then run this command again.",
                    "OK");
                return;
            }

            Animator animator = selected.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                EditorUtility.DisplayDialog(
                    "Valid Humanoid Animator Required",
                    "The selected object needs a valid Humanoid Animator and Avatar. Set the model Rig type to Humanoid in its import settings.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRBody body = Object.FindFirstObjectByType<ApexPhysicalOpenXRBody>(
                FindObjectsInactive.Include);
            if (body == null)
            {
                EditorUtility.DisplayDialog(
                    "Physical OpenXR Rig Not Found",
                    "Create an Apex Physical OpenXR Rig before binding the avatar.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRHand[] hands = Object.FindObjectsByType<ApexPhysicalOpenXRHand>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            ApexPhysicalOpenXRHand leftHand = null;
            ApexPhysicalOpenXRHand rightHand = null;
            for (int i = 0; i < hands.Length; i++)
            {
                ApexPhysicalOpenXRHand hand = hands[i];
                if (hand == null || !hand.transform.IsChildOf(body.transform))
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

            if (leftHand == null || rightHand == null || body.Head == null)
            {
                EditorUtility.DisplayDialog(
                    "Rig References Missing",
                    "Apex could not find the tracked head and both physical hands under the physical OpenXR rig. Rebuild the rig, then bind the avatar again.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind Humanoid to Apex Physical VR Rig");

            Undo.SetTransformParent(selected.transform, body.transform, "Parent Apex VR Avatar");
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;

            ApexPhysicalVRAvatar avatar = selected.GetComponent<ApexPhysicalVRAvatar>();
            if (avatar == null)
            {
                avatar = Undo.AddComponent<ApexPhysicalVRAvatar>(selected);
            }

            avatar.EditorConfigure(
                animator,
                body,
                body.Head,
                leftHand.transform,
                rightHand.transform);

            DisableDebugHandVisuals(leftHand);
            DisableDebugHandVisuals(rightHand);

            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = selected;
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "Bound the selected Humanoid avatar to the Apex Physical OpenXR rig. " +
                "The body, headset, and physical hands now drive the visible avatar.",
                selected);
        }

        [MenuItem(BindPath, true)]
        private static bool ValidateBindSelectedHumanoid()
        {
            return Selection.activeGameObject != null;
        }

        private static void DisableDebugHandVisuals(ApexPhysicalOpenXRHand hand)
        {
            if (hand == null)
            {
                return;
            }

            Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
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

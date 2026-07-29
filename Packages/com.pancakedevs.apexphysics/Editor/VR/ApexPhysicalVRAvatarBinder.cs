using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexPhysicalVRAvatarBinder
    {
        private const string BindPath =
            "Apex Physics Engine/Player/Bind Selected Humanoid to Physical OpenXR Rig";

        [MenuItem(BindPath, false, 121)]
        private static void BindSelectedHumanoid()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "No Humanoid Selected",
                    "Drag the Humanoid model into the scene and select its root in the Hierarchy.",
                    "OK");
                return;
            }

            Animator animator = selected.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                EditorUtility.DisplayDialog(
                    "Valid Humanoid Required",
                    "Set the FBX Rig type to Humanoid, press Apply, and select the scene instance.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRBody body = Object.FindFirstObjectByType<ApexPhysicalOpenXRBody>(
                FindObjectsInactive.Include);
            if (body == null)
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
                    "Apex could not find the tracked head and both physical hands.",
                    "OK");
                return;
            }

            Type avatarType = Type.GetType(
                "PancakeDevs.ApexPhysics.ApexPhysicalVRAvatar, PancakeDevs.ApexPhysics.Runtime");
            if (avatarType == null || !typeof(MonoBehaviour).IsAssignableFrom(avatarType))
            {
                EditorUtility.DisplayDialog(
                    "Avatar Runtime Missing",
                    "The ApexPhysicalVRAvatar runtime component was not imported. Reinstall the latest exact package revision.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind Humanoid to Apex Physical VR Rig");

            Undo.SetTransformParent(selected.transform, body.transform, "Parent Apex VR Avatar");
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;

            Component avatar = selected.GetComponent(avatarType);
            if (avatar == null)
            {
                avatar = Undo.AddComponent(selected, avatarType);
            }

            SerializedObject serializedAvatar = new SerializedObject(avatar);
            serializedAvatar.FindProperty("animator").objectReferenceValue = animator;
            serializedAvatar.FindProperty("physicalBody").objectReferenceValue = body;
            serializedAvatar.FindProperty("headTarget").objectReferenceValue = body.Head;
            serializedAvatar.FindProperty("leftHandTarget").objectReferenceValue = leftHand.transform;
            serializedAvatar.FindProperty("rightHandTarget").objectReferenceValue = rightHand.transform;
            serializedAvatar.ApplyModifiedPropertiesWithoutUndo();

            HideDebugVisuals(leftHand);
            HideDebugVisuals(rightHand);

            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = selected;
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("Bound the selected Humanoid to the Apex Physical OpenXR rig.", selected);
        }

        [MenuItem(BindPath, true)]
        private static bool ValidateBindSelectedHumanoid()
        {
            return Selection.activeGameObject != null;
        }

        private static void HideDebugVisuals(ApexPhysicalOpenXRHand hand)
        {
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

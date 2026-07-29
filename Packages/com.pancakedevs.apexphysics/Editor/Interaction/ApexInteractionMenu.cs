using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexInteractionMenu
    {
        private const string MakeGrabbablePath =
            "Apex Physics Engine/Player/Make Selected Object Player Grabbable";
        private const string AddGrabPointPath =
            "Apex Physics Engine/Player/Add Grab Point to Selected Grabbable";

        [MenuItem(MakeGrabbablePath, false, 120)]
        private static void MakeSelectedObjectPlayerGrabbable()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Undo.SetCurrentGroupName("Make Apex Player Grabbable");
            int undoGroup = Undo.GetCurrentGroup();

            if (selected.GetComponentInChildren<Collider>() == null)
            {
                Undo.AddComponent<BoxCollider>(selected);
            }

            Rigidbody body = selected.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(selected);
            }

            body.isKinematic = false;
            body.useGravity = true;

            if (selected.GetComponent<ApexBody>() == null)
            {
                Undo.AddComponent<ApexBody>(selected);
            }

            ApexGrabbable grabbable = selected.GetComponent<ApexGrabbable>();
            if (grabbable == null)
            {
                grabbable = Undo.AddComponent<ApexGrabbable>(selected);
            }

            grabbable.RefreshGrabData();
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(AddGrabPointPath, false, 121)]
        private static void AddGrabPoint()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            ApexGrabbable grabbable = selected.GetComponentInParent<ApexGrabbable>();
            if (grabbable == null)
            {
                EditorUtility.DisplayDialog(
                    "Apex Player Grabbable",
                    "Make the object Player Grabbable before adding a grab point.",
                    "OK");
                return;
            }

            GameObject pointObject = new GameObject("Apex Grab Point");
            Undo.RegisterCreatedObjectUndo(pointObject, "Add Apex Grab Point");
            pointObject.transform.SetParent(grabbable.transform, false);
            Undo.AddComponent<ApexGrabPoint>(pointObject);
            grabbable.RefreshGrabData();
            Selection.activeGameObject = pointObject;
        }

        [MenuItem(MakeGrabbablePath, true)]
        private static bool ValidateMakeGrabbable()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(AddGrabPointPath, true)]
        private static bool ValidateAddGrabPoint()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponentInParent<ApexGrabbable>() != null;
        }
    }
}

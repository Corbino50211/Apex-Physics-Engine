using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexInteractionMenu
    {
        private const string MakeGrabbablePath = "Apex Physics Engine/Grabbing/Make Selected Object Grabbable";
        private const string MakeGrabberPath = "Apex Physics Engine/Grabbing/Make Selected Object a Grabber";
        private const string AddGrabPointPath = "Apex Physics Engine/Grabbing/Add Grab Point";

        [MenuItem(MakeGrabbablePath, false, 20)]
        private static void MakeSelectedObjectGrabbable()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Undo.SetCurrentGroupName("Make Apex Grabbable");
            int undoGroup = Undo.GetCurrentGroup();

            if (selected.GetComponentInChildren<Collider>() == null)
            {
                Undo.AddComponent<BoxCollider>(selected);
            }

            if (selected.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(selected);
            }

            if (selected.GetComponent<ApexBody>() == null)
            {
                Undo.AddComponent<ApexBody>(selected);
            }

            if (selected.GetComponent<ApexGrabbable>() == null)
            {
                Undo.AddComponent<ApexGrabbable>(selected);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(MakeGrabberPath, false, 21)]
        private static void MakeSelectedObjectGrabber()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            if (selected.GetComponent<ApexGrabber>() == null)
            {
                Undo.AddComponent<ApexGrabber>(selected);
            }

            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(AddGrabPointPath, false, 22)]
        private static void AddGrabPoint()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            GameObject pointObject = new GameObject("Apex Grab Point");
            Undo.RegisterCreatedObjectUndo(pointObject, "Add Apex Grab Point");
            pointObject.transform.SetParent(selected.transform, false);
            Undo.AddComponent<ApexGrabPoint>(pointObject);

            ApexGrabbable grabbable = selected.GetComponentInParent<ApexGrabbable>();
            grabbable?.RefreshGrabData();

            Selection.activeGameObject = pointObject;
        }

        [MenuItem(MakeGrabbablePath, true)]
        [MenuItem(MakeGrabberPath, true)]
        [MenuItem(AddGrabPointPath, true)]
        private static bool ValidateSelection()
        {
            return Selection.activeGameObject != null;
        }
    }
}

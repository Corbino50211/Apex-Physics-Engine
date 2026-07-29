using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexBodyMenu
    {
        private const string MenuPath = "Apex Physics Engine/Bodies/Make Selected Object an Apex Body";

        [MenuItem(MenuPath, false, 10)]
        private static void MakeSelectedObjectAnApexBody()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Undo.SetCurrentGroupName("Make Apex Body");
            int undoGroup = Undo.GetCurrentGroup();

            if (selected.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(selected);
            }

            if (selected.GetComponent<ApexBody>() == null)
            {
                Undo.AddComponent<ApexBody>(selected);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMakeSelectedObjectAnApexBody()
        {
            return Selection.activeGameObject != null;
        }
    }
}

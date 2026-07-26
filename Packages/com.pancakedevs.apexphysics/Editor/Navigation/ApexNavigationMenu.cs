using PancakeDevs.ApexPhysics;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexNavigationMenu
    {
        private const string CreateSurfacePath = "GameObject/Apex Physics/Create Navigation Surface";
        private const string MakeNavigatorPath = "GameObject/Apex Physics/Make Selected Object an NPC Navigator";

        [MenuItem(CreateSurfacePath, false, 40)]
        private static void CreateNavigationSurface()
        {
            GameObject navigationObject = new GameObject("Apex Navigation Surface");
            Undo.RegisterCreatedObjectUndo(navigationObject, "Create Apex Navigation Surface");

            Undo.AddComponent<NavMeshSurface>(navigationObject);
            Undo.AddComponent<ApexNavMeshAutoBaker>(navigationObject);

            GameObject parent = Selection.activeGameObject;
            if (parent != null)
            {
                Undo.SetTransformParent(
                    navigationObject.transform,
                    parent.transform,
                    "Parent Apex Navigation Surface");
            }

            Selection.activeGameObject = navigationObject;
            EditorGUIUtility.PingObject(navigationObject);
        }

        [MenuItem(MakeNavigatorPath, false, 41)]
        private static void MakeSelectedObjectNavigator()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Undo.SetCurrentGroupName("Make Apex NPC Navigator");
            int undoGroup = Undo.GetCurrentGroup();

            if (selected.GetComponent<NavMeshAgent>() == null)
            {
                Undo.AddComponent<NavMeshAgent>(selected);
            }

            if (selected.GetComponent<ApexNPCNavigator>() == null)
            {
                Undo.AddComponent<ApexNPCNavigator>(selected);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(MakeNavigatorPath, true)]
        private static bool ValidateMakeSelectedObjectNavigator()
        {
            return Selection.activeGameObject != null;
        }
    }
}

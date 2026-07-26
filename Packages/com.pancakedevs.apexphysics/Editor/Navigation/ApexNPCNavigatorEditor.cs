using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexNPCNavigator))]
    public sealed class ApexNPCNavigatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexNPCNavigator navigator = (ApexNPCNavigator)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex NPC Navigation", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to inspect live path data. Version 0.0.3 calculates paths but does not yet apply physical walking forces.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("On NavMesh", navigator.IsOnNavMesh ? "Yes" : "No");
            EditorGUILayout.LabelField("Has Path", navigator.HasPath ? "Yes" : "No");
            EditorGUILayout.LabelField("Path Pending", navigator.IsPathPending ? "Yes" : "No");
            EditorGUILayout.LabelField("Path Status", navigator.PathStatus.ToString());
            EditorGUILayout.LabelField("Reached", navigator.HasReachedDestination ? "Yes" : "No");
            EditorGUILayout.FloatField("Remaining Distance", navigator.RemainingDistance);
            EditorGUILayout.Vector3Field("Desired Velocity", navigator.DesiredVelocity);
            EditorGUILayout.Vector3Field("Steering Target", navigator.SteeringTarget);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bind to NavMesh"))
                {
                    navigator.TryBindToNavMesh();
                }

                using (new EditorGUI.DisabledScope(navigator.Target == null))
                {
                    if (GUILayout.Button("Repath to Target"))
                    {
                        navigator.SetTarget(navigator.Target);
                    }
                }
            }

            if (GUILayout.Button("Clear Destination"))
            {
                navigator.ClearDestination();
            }

            if (GUI.changed)
            {
                Repaint();
            }
        }
    }
}

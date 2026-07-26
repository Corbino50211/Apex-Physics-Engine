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
            ApexNPCMotor motor = navigator.GetComponent<ApexNPCMotor>();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex NPC Navigation", EditorStyles.boldLabel);

            if (motor == null)
            {
                EditorGUILayout.HelpBox(
                    "This object can calculate a path, but it cannot move because ApexNPCMotor is missing.",
                    MessageType.Warning);

                if (GUILayout.Button("Add Physical NPC Motor"))
                {
                    Undo.AddComponent<ApexNPCMotor>(navigator.gameObject);
                    EditorUtility.SetDirty(navigator.gameObject);
                    return;
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    motor != null
                        ? "This NPC has a physical motor. Enter Play Mode to test movement and inspect live path data."
                        : "Add the physical motor above, then enter Play Mode.",
                    motor != null ? MessageType.Info : MessageType.Warning);
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

            if (motor != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Physical Motor", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Moving", motor.IsMoving ? "Yes" : "No");
                EditorGUILayout.Vector3Field("Requested Velocity", motor.RequestedVelocity);

                Rigidbody rigidbody = motor.Body != null ? motor.Body.Rigidbody : null;
                if (rigidbody != null)
                {
                    EditorGUILayout.Vector3Field("Rigidbody Velocity", rigidbody.velocity);
                }
            }

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

using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexNPCBrain))]
    public sealed class ApexNPCBrainEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexNPCBrain brain = (ApexNPCBrain)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex NPC Behavior", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "The NPC will use its Starting Mode when Play Mode begins. Wander selects random NavMesh points around its starting position. Hard impacts can temporarily switch it to Chase.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Current Mode", brain.CurrentMode.ToString());
            EditorGUILayout.ObjectField("Chase Target", brain.ChaseTarget, typeof(Transform), true);
            EditorGUILayout.Vector3Field("Home Position", brain.HomePosition);
            EditorGUILayout.FloatField("Chase Time Remaining", brain.RemainingImpactChaseTime);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Idle"))
                {
                    brain.SetMode(ApexNPCBehaviorMode.Idle);
                }

                if (GUILayout.Button("Wander"))
                {
                    brain.SetMode(ApexNPCBehaviorMode.Wander);
                }

                using (new EditorGUI.DisabledScope(brain.ChaseTarget == null))
                {
                    if (GUILayout.Button("Chase"))
                    {
                        brain.SetMode(ApexNPCBehaviorMode.Chase);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Wander Point"))
                {
                    brain.PickNewWanderDestination();
                }

                if (GUILayout.Button("Set Home Here"))
                {
                    brain.SetHomeHere();
                }
            }

            if (GUI.changed)
            {
                Repaint();
            }
        }
    }
}

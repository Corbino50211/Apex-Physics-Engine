using System;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexDestructible))]
    public sealed class ApexDestructibleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexDestructible destructible = (ApexDestructible)target;
            EditorGUILayout.Space();
            DrawTriggerExplanation(destructible);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Fracture", EditorStyles.boldLabel);

            if (destructible.HasGeneratedFracture)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Chunks", destructible.ChunkCount.ToString());
                    EditorGUILayout.LabelField(
                        "Generated Assets",
                        string.IsNullOrWhiteSpace(destructible.GeneratedAssetFolder)
                            ? "Unknown"
                            : destructible.GeneratedAssetFolder);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No generated chunks are assigned. Generate a fracture before the object can break.",
                    MessageType.Warning);
            }

            if (!Application.isPlaying)
            {
                if (GUILayout.Button(
                        destructible.HasGeneratedFracture
                            ? "Rebuild Fracture..."
                            : "Generate Fracture...",
                        GUILayout.Height(34f)))
                {
                    ApexDestructionWindow.OpenFor(destructible.gameObject);
                }

                using (new EditorGUI.DisabledScope(!destructible.HasGeneratedFracture))
                {
                    if (GUILayout.Button("Clear Generated Fracture"))
                    {
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Clear Apex Fracture",
                            "Remove the generated chunk hierarchy and its generated mesh assets?",
                            "Clear",
                            "Cancel");
                        if (confirmed)
                        {
                            ApexDestructionGenerator.ClearGeneratedFracture(destructible, true);
                        }
                    }
                }

                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Test", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fractured", destructible.IsFractured ? "Yes" : "No");
            EditorGUILayout.LabelField(
                "Released Chunks",
                destructible.ReleasedChunkCount + " / " + destructible.ChunkCount);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!destructible.HasGeneratedFracture))
                {
                    if (GUILayout.Button("Break At Center"))
                    {
                        float testImpulse = destructible.ThresholdMeasurement ==
                                            ApexImpactThresholdMeasurement.EstimatedForce
                            ? destructible.BreakThreshold * Mathf.Max(0.0001f, Time.fixedDeltaTime)
                            : destructible.BreakThreshold;
                        destructible.BreakAt(
                            destructible.transform.position,
                            Vector3.up,
                            testImpulse);
                    }

                    if (GUILayout.Button("Break All"))
                    {
                        destructible.BreakAll();
                    }
                }
            }
        }

        private static void DrawTriggerExplanation(ApexDestructible destructible)
        {
            if (destructible.FractureTrigger == ApexFractureTrigger.ManualOnly)
            {
                EditorGUILayout.HelpBox(
                    "Fracture geometry is generated in Edit Mode. Runtime activation is Manual Only, so collisions will not break this object. Use BreakAt, BreakAll, a UnityEvent, or the Play Mode test buttons.",
                    MessageType.Info);
                return;
            }

            string unit = destructible.ThresholdMeasurement ==
                          ApexImpactThresholdMeasurement.EstimatedForce
                ? "newtons (estimated as collision impulse / Fixed Timestep)"
                : "newton-seconds of collision impulse";
            EditorGUILayout.HelpBox(
                $"Fracture geometry is generated in Edit Mode. During Play Mode this object automatically fractures when an impact reaches {destructible.BreakThreshold:0.###} {unit}.",
                MessageType.Info);
        }
    }
}

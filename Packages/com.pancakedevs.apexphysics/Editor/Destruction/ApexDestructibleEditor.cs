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
                        destructible.BreakAt(
                            destructible.transform.position,
                            Vector3.up,
                            destructible.BreakImpulse);
                    }

                    if (GUILayout.Button("Break All"))
                    {
                        destructible.BreakAll();
                    }
                }
            }
        }
    }
}

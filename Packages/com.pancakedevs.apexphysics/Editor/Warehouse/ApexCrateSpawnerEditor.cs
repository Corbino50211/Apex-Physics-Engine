using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexCrateSpawner))]
    public sealed class ApexCrateSpawnerEditor : UnityEditor.Editor
    {
        private ApexCratePreviewRenderer previewRenderer;

        private void OnEnable()
        {
            previewRenderer = new ApexCratePreviewRenderer();
        }

        private void OnDisable()
        {
            previewRenderer?.Dispose();
            previewRenderer = null;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexCrateSpawner spawner = (ApexCrateSpawner)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Crate Spawner", EditorStyles.boldLabel);

            ApexSpawnableCrate resolved = spawner.ResolveCrate();
            EditorGUILayout.LabelField("Resolved Crate", resolved != null ? resolved.Title : "Missing");
            EditorGUILayout.LabelField("Barcode", string.IsNullOrWhiteSpace(spawner.Barcode) ? "None" : spawner.Barcode);

            if (resolved == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Spawnable Crate directly, or make sure an Apex Warehouse registers the matching barcode before this spawner runs.",
                    MessageType.Warning);
            }
            else if (resolved.PreviewPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "The resolved crate has no prefab available for its Void Preview.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    spawner.ShowVoidPreview
                        ? "The crate model is displayed in the Scene view with the Apex Void Preview material."
                        : "Enable Show Void Preview to display the crate model in the Scene view.",
                    MessageType.Info);

                if (GUILayout.Button("Frame Preview Model"))
                {
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "The default End Of Frame timing creates the prefab after the level and warehouse finish loading.",
                    MessageType.Info);
                SceneView.RepaintAll();
                return;
            }

            EditorGUILayout.LabelField("Has Spawned", spawner.HasSpawned ? "Yes" : "No");
            EditorGUILayout.LabelField("Live Instances", spawner.SpawnedInstances.Count.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn Now"))
                {
                    spawner.ResetSpawnState();
                    spawner.SpawnAll();
                }

                if (GUILayout.Button("Clear Spawned"))
                {
                    spawner.ClearSpawned();
                }
            }

            if (GUILayout.Button("Reset Spawn State"))
            {
                spawner.ResetSpawnState();
            }

            Repaint();
        }

        public override bool HasPreviewGUI()
        {
            ApexCrateSpawner spawner = target as ApexCrateSpawner;
            ApexSpawnableCrate crate = spawner != null ? spawner.ResolveCrate() : null;
            return previewRenderer != null && previewRenderer.HasPreview(crate);
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Spawned Crate Preview");
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            ApexCrateSpawner spawner = target as ApexCrateSpawner;
            ApexSpawnableCrate crate = spawner != null ? spawner.ResolveCrate() : null;
            if (crate == null || previewRenderer == null)
            {
                return;
            }

            previewRenderer.DrawInspectorPreview(crate, rect, background, Repaint);
        }
    }
}

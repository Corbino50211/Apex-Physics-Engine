using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexCrateSpawner))]
    public sealed class ApexCrateSpawnerEditor : UnityEditor.Editor
    {
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

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "The default End Of Frame timing creates the prefab after the level and warehouse finish loading.",
                    MessageType.Info);
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
    }
}

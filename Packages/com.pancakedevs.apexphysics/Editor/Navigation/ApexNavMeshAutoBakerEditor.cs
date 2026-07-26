using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexNavMeshAutoBaker))]
    public sealed class ApexNavMeshAutoBakerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexNavMeshAutoBaker baker = (ApexNavMeshAutoBaker)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Navigation Tools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Dirty", baker.IsDirty ? "Yes" : "No");
            EditorGUILayout.LabelField("Build Count", baker.BuildCount.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Now"))
                {
                    BuildAndMarkSceneDirty(baker, false);
                }

                using (new EditorGUI.DisabledScope(!baker.IsDirty))
                {
                    if (GUILayout.Button("Build If Dirty"))
                    {
                        BuildAndMarkSceneDirty(baker, true);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mark Dirty"))
                {
                    Undo.RecordObject(baker, "Mark Apex NavMesh Dirty");
                    baker.MarkDirty();
                    EditorUtility.SetDirty(baker);
                }

                if (GUILayout.Button("Clear NavMesh"))
                {
                    Undo.RecordObject(baker, "Clear Apex NavMesh");
                    baker.Clear();
                    MarkSceneDirty(baker);
                }
            }
        }

        private static void BuildAndMarkSceneDirty(ApexNavMeshAutoBaker baker, bool onlyIfNeeded)
        {
            bool built = onlyIfNeeded ? baker.BuildIfNeeded() : baker.BuildNow();
            if (built)
            {
                MarkSceneDirty(baker);
            }
        }

        private static void MarkSceneDirty(ApexNavMeshAutoBaker baker)
        {
            if (baker != null && baker.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(baker.gameObject.scene);
            }
        }
    }

    [InitializeOnLoad]
    internal static class ApexNavMeshPlayModeBaker
    {
        static ApexNavMeshPlayModeBaker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            ApexNavMeshAutoBaker[] bakers = Resources.FindObjectsOfTypeAll<ApexNavMeshAutoBaker>();
            for (int i = 0; i < bakers.Length; i++)
            {
                ApexNavMeshAutoBaker baker = bakers[i];
                if (baker == null ||
                    !baker.ShouldBuildBeforePlayMode ||
                    !baker.gameObject.scene.IsValid() ||
                    !baker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (baker.BuildIfNeeded())
                {
                    EditorSceneManager.MarkSceneDirty(baker.gameObject.scene);
                }
            }
        }
    }
}

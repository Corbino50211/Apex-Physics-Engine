using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexWaterVolume))]
    public sealed class ApexWaterVolumeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Apex water physics is render-pipeline independent. The built-in Simple material works immediately; import the optional Apex Water URP Visuals sample for depth color, foam, caustics, and refraction.",
                MessageType.Info);

            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            ApexWaterVolume volume = (ApexWaterVolume)target;
            DrawTransformWarning(volume);
            DrawSurfaceTools(volume);
            DrawRuntimeStatus(volume);
        }

        private static void DrawTransformWarning(ApexWaterVolume volume)
        {
            Vector3 euler = volume.transform.eulerAngles;
            float x = Mathf.DeltaAngle(0f, euler.x);
            float z = Mathf.DeltaAngle(0f, euler.z);
            if (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f)
            {
                EditorGUILayout.HelpBox(
                    "Apex water surfaces are designed to remain upright. Rotate around Y only; X/Z rotation can make physics height and the visual surface disagree.",
                    MessageType.Warning);
            }
        }

        private static void DrawSurfaceTools(ApexWaterVolume volume)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Water Authoring", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                bool urpAvailable = ApexWaterAssetUtility.HasUrpSampleShader();
                if (GUILayout.Button(
                        urpAvailable
                            ? "Generate / Rebuild Surface (URP Visuals)"
                            : "Generate / Rebuild Surface",
                        GUILayout.Height(36f)))
                {
                    Undo.RegisterCompleteObjectUndo(
                        volume.gameObject,
                        "Generate Apex Water Surface");
                    bool success = ApexWaterAssetUtility.GenerateSurfaceAssets(
                        volume,
                        urpAvailable,
                        out string message);
                    if (success)
                    {
                        Debug.Log(message, volume);
                    }
                    else
                    {
                        Debug.LogWarning(message, volume);
                    }
                }

                MeshFilter filter = volume.GetComponent<MeshFilter>();
                string currentMeshPath = filter != null && filter.sharedMesh != null
                    ? AssetDatabase.GetAssetPath(filter.sharedMesh)
                    : string.Empty;
                bool generatedSurfaceIsAssigned =
                    !string.IsNullOrWhiteSpace(volume.GeneratedSurfaceMeshPath) &&
                    currentMeshPath == volume.GeneratedSurfaceMeshPath;

                using (new EditorGUI.DisabledScope(generatedSurfaceIsAssigned))
                {
                    if (GUILayout.Button("Recapture Logical Bounds From Current Mesh"))
                    {
                        Undo.RecordObject(volume, "Recapture Apex Water Bounds");
                        volume.CaptureBoundsFromCurrentMesh();
                        volume.RefreshBounds();
                        EditorUtility.SetDirty(volume);
                    }
                }

                if (generatedSurfaceIsAssigned)
                {
                    EditorGUILayout.HelpBox(
                        "The current mesh is Apex's thin generated surface, so logical-bound recapture is disabled. Set the Volume fields directly or assign the original source mesh before recapturing.",
                        MessageType.None);
                }

                if (GUILayout.Button("Apply Water Properties To Renderer"))
                {
                    volume.ApplyMaterialProperties();
                }

                bool hasGeneratedAssets =
                    !string.IsNullOrWhiteSpace(volume.GeneratedSurfaceMeshPath) ||
                    !string.IsNullOrWhiteSpace(volume.GeneratedMaterialPath);
                using (new EditorGUI.DisabledScope(!hasGeneratedAssets))
                {
                    if (GUILayout.Button("Clear Generated Water Assets"))
                    {
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Clear Apex Water Assets",
                            "Remove the generated surface mesh and generated material? The logical water volume component will remain.",
                            "Clear",
                            "Cancel");
                        if (confirmed)
                        {
                            ApexWaterAssetUtility.ClearGeneratedSurface(volume);
                        }
                    }
                }
            }

            if (!ApexWaterAssetUtility.HasUrpSampleShader())
            {
                EditorGUILayout.HelpBox(
                    "Optional: import the 'Apex Water URP Visuals' sample from Package Manager for the advanced URP shader. Apex never changes Depth Texture or Opaque Texture settings automatically.",
                    MessageType.None);
            }
        }

        private static void DrawRuntimeStatus(ApexWaterVolume volume)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Query", EditorStyles.boldLabel);
            Vector3 probe = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : volume.transform.position;
            EditorGUILayout.LabelField(
                "Surface Height At Probe",
                volume.GetWaterHeight(probe).ToString("0.000"));
            EditorGUILayout.LabelField("Registered Volumes", ApexWaterManager.Volumes.Count.ToString());
        }
    }

    [CustomEditor(typeof(ApexBuoyantBody))]
    public sealed class ApexBuoyantBodyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ApexBuoyantBody buoyant = (ApexBuoyantBody)target;
            EditorGUILayout.Space();
            if (!Application.isPlaying && GUILayout.Button("Generate Points From Colliders"))
            {
                Undo.RecordObject(buoyant, "Generate Apex Buoyancy Points");
                buoyant.GeneratePointsFromColliders(true);
                EditorUtility.SetDirty(buoyant);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("In Water", buoyant.IsInWater ? "Yes" : "No");
                EditorGUILayout.LabelField(
                    "Submersion",
                    buoyant.SubmersionFraction.ToString("P0"));
            }
        }
    }

    [CustomEditor(typeof(ApexSwimmer))]
    public sealed class ApexSwimmerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "ApexSwimmer is input-agnostic. Feed SetMoveInput, SetVerticalInput, and SetSprint from Input System actions, AI, networking, or VR. Add ApexLegacySwimInput only for quick legacy-input testing.",
                MessageType.Info);
            DrawDefaultInspector();

            if (Application.isPlaying)
            {
                ApexSwimmer swimmer = (ApexSwimmer)target;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Swimming", swimmer.IsSwimming ? "Yes" : "No");
                EditorGUILayout.LabelField(
                    "Head Underwater",
                    swimmer.IsHeadUnderwater ? "Yes" : "No");
                EditorGUILayout.LabelField("Oxygen", swimmer.Oxygen01.ToString("P0"));
            }
        }
    }
}

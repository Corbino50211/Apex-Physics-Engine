using System.IO;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexCrate), true)]
    public sealed class ApexCrateEditor : UnityEditor.Editor
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

            ApexCrate crate = (ApexCrate)target;
            crate.EnsureBarcode();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Crate Identity", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Kind", crate.Kind.ToString());
            EditorGUILayout.LabelField("Barcode");
            EditorGUILayout.SelectableLabel(crate.Barcode, EditorStyles.textField, GUILayout.Height(20f));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Barcode"))
                {
                    EditorGUIUtility.systemCopyBuffer = crate.Barcode;
                }

                if (GUILayout.Button("Regenerate Barcode") &&
                    EditorUtility.DisplayDialog(
                        "Regenerate Apex Barcode?",
                        "Existing spawners, saves, or network references using this barcode will stop resolving this crate.",
                        "Regenerate",
                        "Cancel"))
                {
                    Undo.RecordObject(crate, "Regenerate Apex Crate Barcode");
                    crate.RegenerateBarcode();
                    EditorUtility.SetDirty(crate);
                    AssetDatabase.SaveAssets();
                }
            }

            if (!(crate is ApexSpawnableCrate spawnable))
            {
                return;
            }

            if (spawnable.Prefab == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a prefab before this crate can be spawned or previewed.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Void Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The crate preview uses the spawn prefab unless Preview Prefab overrides it. " +
                "Drag inside the Preview panel below to rotate the model.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Preview Model"))
                {
                    EditorGUIUtility.PingObject(spawnable.PreviewPrefab);
                    Selection.activeObject = spawnable.PreviewPrefab;
                }

                if (GUILayout.Button("Create Editable Void Material"))
                {
                    CreateAndAssignVoidMaterial(spawnable);
                }
            }
        }

        public override bool HasPreviewGUI()
        {
            return target is ApexSpawnableCrate spawnable &&
                   previewRenderer != null &&
                   previewRenderer.HasPreview(spawnable);
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Apex Void Crate Preview");
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (!(target is ApexSpawnableCrate spawnable) || previewRenderer == null)
            {
                return;
            }

            previewRenderer.DrawInspectorPreview(spawnable, rect, background, Repaint);
        }

        private static void CreateAndAssignVoidMaterial(ApexSpawnableCrate crate)
        {
            Material material = ApexCratePreviewRenderer.CreateEditableVoidMaterial();
            if (material == null)
            {
                EditorUtility.DisplayDialog(
                    "Apex Void Preview",
                    "The Apex Void Preview shader could not be found. Reimport or update the Apex package.",
                    "OK");
                return;
            }

            string cratePath = AssetDatabase.GetAssetPath(crate);
            string directory = string.IsNullOrWhiteSpace(cratePath)
                ? "Assets"
                : Path.GetDirectoryName(cratePath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = "Assets";
            }

            string safeTitle = string.IsNullOrWhiteSpace(crate.Title)
                ? crate.name
                : crate.Title;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safeTitle = safeTitle.Replace(invalid, '_');
            }

            string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{directory}/{safeTitle} Void Preview.mat");
            AssetDatabase.CreateAsset(material, materialPath);

            Undo.RecordObject(crate, "Assign Apex Void Preview Material");
            crate.SetPreviewMaterial(material);
            EditorUtility.SetDirty(crate);
            AssetDatabase.SaveAssets();
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }
    }
}

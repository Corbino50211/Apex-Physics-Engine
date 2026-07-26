using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexCrate), true)]
    public sealed class ApexCrateEditor : UnityEditor.Editor
    {
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

            if (crate is ApexSpawnableCrate spawnable && spawnable.Prefab == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a prefab before this crate can be spawned.",
                    MessageType.Warning);
            }
        }
    }
}

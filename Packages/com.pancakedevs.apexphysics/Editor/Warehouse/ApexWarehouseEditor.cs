using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexWarehouse))]
    public sealed class ApexWarehouseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexWarehouse warehouse = (ApexWarehouse)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Warehouse", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Add Pallet assets above. Their crates are registered when the Warehouse awakens.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Registered Crates", warehouse.RegisteredCrateCount.ToString());

            if (GUILayout.Button("Rebuild Registry"))
            {
                warehouse.RebuildRegistry();
            }

            foreach (ApexCrate crate in warehouse.RegisteredCrates)
            {
                if (crate == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(crate, typeof(ApexCrate), false);
                EditorGUILayout.SelectableLabel(crate.Barcode, GUILayout.Height(18f));
                EditorGUILayout.EndHorizontal();
            }

            Repaint();
        }
    }
}

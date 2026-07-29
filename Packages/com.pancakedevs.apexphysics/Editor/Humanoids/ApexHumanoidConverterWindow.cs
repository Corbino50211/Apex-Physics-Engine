using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    /// <summary>
    /// Legacy humanoid conversion window retained for project compatibility.
    /// It is intentionally no longer exposed through the Apex menu because the
    /// current desktop workflow uses ApexPCPlayerMotor and ApexPCPlayerInteraction.
    /// </summary>
    public sealed class ApexHumanoidConverterWindow : EditorWindow
    {
        [SerializeField] private GameObject character;
        [SerializeField] private bool saveAsPrefab = true;
        [SerializeField] private bool createSpawnableCrate;

        private void OnSelectionChange()
        {
            if (Selection.activeObject is GameObject selected)
            {
                character = selected;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Legacy Apex Physical NPC Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This converter is retained only so older projects and editor references remain valid. " +
                "Use Apex Physics Engine > Player > Create PC Player Rig for the current playable desktop rig.",
                MessageType.Warning);

            EditorGUILayout.Space();
            character = (GameObject)EditorGUILayout.ObjectField(
                "Humanoid Character",
                character,
                typeof(GameObject),
                true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Assets", EditorStyles.boldLabel);
            saveAsPrefab = EditorGUILayout.Toggle("Save Converted Prefab", saveAsPrefab);
            using (new EditorGUI.DisabledScope(!saveAsPrefab))
            {
                createSpawnableCrate = EditorGUILayout.Toggle(
                    "Create Warehouse Crate",
                    createSpawnableCrate);
            }

            if (!saveAsPrefab)
            {
                createSpawnableCrate = false;
            }

            EditorGUILayout.Space();
            bool valid = ApexHumanoidConverter.CanConvert(character, out string reason);
            EditorGUILayout.HelpBox(
                valid ? "Legacy humanoid validation passed." : reason,
                valid ? MessageType.Info : MessageType.Warning);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Build Legacy Physical NPC", GUILayout.Height(36f)))
                {
                    ApexPhysicalHumanoid converted = ApexHumanoidConverter.Convert(
                        character,
                        ApexPhysicalHumanoidMode.PhysicalNPC,
                        saveAsPrefab,
                        createSpawnableCrate);
                    if (converted != null)
                    {
                        converted.EnsurePCPhysicalCharacter();
                        EnsureMotorFitter(converted);
                        character = converted.gameObject;
                    }
                }
            }
        }

        private static void EnsureMotorFitter(ApexPhysicalHumanoid humanoid)
        {
            ApexPCMotorFitter fitter = humanoid.GetComponent<ApexPCMotorFitter>();
            if (fitter == null)
            {
                fitter = Undo.AddComponent<ApexPCMotorFitter>(humanoid.gameObject);
            }

            Undo.RecordObject(fitter, "Fit Apex PC Character Motor");
            fitter.Configure(humanoid);
            EditorUtility.SetDirty(fitter);
            EditorUtility.SetDirty(humanoid);
        }
    }
}

using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    /// <summary>
    /// Builder for the current Apex physical NPC system.
    /// The old generic PC physical-character window menu remains hidden, while the
    /// selected-Humanoid NPC workflow stays available from the Characters menu.
    /// </summary>
    public sealed class ApexHumanoidConverterWindow : EditorWindow
    {
        private const string MenuRoot = "Apex Physics Engine/Characters/";

        [SerializeField] private GameObject character;
        [SerializeField] private bool saveAsPrefab = true;
        [SerializeField] private bool createSpawnableCrate;

        [MenuItem(MenuRoot + "Build Selected Humanoid as PC Physical NPC", false, 6)]
        private static void BuildSelectedPcNpc()
        {
            BuildSelection(true, false);
        }

        [MenuItem(MenuRoot + "Build Selected Humanoid as PC Physical NPC", true)]
        private static bool ValidateBuildSelectedPcNpc()
        {
            return Selection.activeObject is GameObject candidate &&
                   ApexHumanoidConverter.CanConvert(candidate, out _);
        }

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
            EditorGUILayout.LabelField("Apex PC Physical NPC", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates the current Apex physical NPC with a capsule motor, hidden animated target, " +
                "visible physical body, navigation, impact ragdoll, and get-up recovery.",
                MessageType.Info);

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
                valid ? "Humanoid validation passed." : reason,
                valid ? MessageType.Info : MessageType.Warning);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Build PC Physical NPC", GUILayout.Height(42f)))
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

        private static void BuildSelection(bool saveAsPrefab, bool createCrate)
        {
            GameObject selected = Selection.activeObject as GameObject;
            ApexPhysicalHumanoid converted = ApexHumanoidConverter.Convert(
                selected,
                ApexPhysicalHumanoidMode.PhysicalNPC,
                saveAsPrefab,
                createCrate);
            if (converted != null)
            {
                converted.EnsurePCPhysicalCharacter();
                EnsureMotorFitter(converted);
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

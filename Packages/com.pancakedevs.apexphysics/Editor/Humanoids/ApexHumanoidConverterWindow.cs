using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    public sealed class ApexHumanoidConverterWindow : EditorWindow
    {
        private const string MenuRoot = "Apex Physics Engine/Characters/";

        [SerializeField] private GameObject character;
        [SerializeField] private bool saveAsPrefab = true;
        [SerializeField] private bool createSpawnableCrate;

        [MenuItem(MenuRoot + "Build PC Physical Character...", false, 5)]
        public static void Open()
        {
            ApexHumanoidConverterWindow window = GetWindow<ApexHumanoidConverterWindow>();
            window.titleContent = new GUIContent("Apex PC Character");
            window.minSize = new Vector2(450f, 310f);
            window.character = Selection.activeObject as GameObject;
            window.Show();
        }

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
            EditorGUILayout.LabelField("Apex PC Physical Character", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "PC-first builder. It creates one capsule motor, a hidden animated target, " +
                "a visible physical body, NavMesh AI, impact ragdoll, and get-up recovery. " +
                "VR tracking and physical-player generation are intentionally postponed.",
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
            EditorGUILayout.HelpBox(
                "Runtime states: Active → Ragdoll → Getting Up → Active. " +
                "While Active, the animated pose is copied directly to the visible body for stability. " +
                "A hard external Rigidbody impact releases the complete ragdoll.",
                MessageType.None);

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
            converted?.EnsurePCPhysicalCharacter();
        }
    }
}

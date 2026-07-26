using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    public sealed class ApexHumanoidConverterWindow : EditorWindow
    {
        private const string MenuRoot = "Apex Physics Engine/Characters/";

        [SerializeField] private GameObject character;
        [SerializeField] private ApexPhysicalHumanoidMode mode =
            ApexPhysicalHumanoidMode.PhysicalNPC;
        [SerializeField] private bool saveAsPrefab = true;
        [SerializeField] private bool createSpawnableCrate;

        [MenuItem(MenuRoot + "Convert Selected Humanoid...", false, 5)]
        public static void Open()
        {
            ApexHumanoidConverterWindow window = GetWindow<ApexHumanoidConverterWindow>();
            window.titleContent = new GUIContent("Apex Humanoid Converter");
            window.minSize = new Vector2(430f, 300f);
            window.character = Selection.activeObject as GameObject;
            window.Show();
        }

        [MenuItem(MenuRoot + "Convert Selected Humanoid to Physical NPC", false, 6)]
        private static void ConvertSelectedToNpc()
        {
            ConvertSelection(ApexPhysicalHumanoidMode.PhysicalNPC);
        }

        [MenuItem(MenuRoot + "Convert Selected Humanoid to Physical Player", false, 7)]
        private static void ConvertSelectedToPlayer()
        {
            ConvertSelection(ApexPhysicalHumanoidMode.PhysicalPlayer);
        }

        [MenuItem(MenuRoot + "Convert Selected Humanoid to Active Ragdoll", false, 8)]
        private static void ConvertSelectedToRagdoll()
        {
            ConvertSelection(ApexPhysicalHumanoidMode.ActiveRagdoll);
        }

        [MenuItem(MenuRoot + "Convert Selected Humanoid to Physical NPC", true)]
        [MenuItem(MenuRoot + "Convert Selected Humanoid to Physical Player", true)]
        [MenuItem(MenuRoot + "Convert Selected Humanoid to Active Ragdoll", true)]
        private static bool ValidateDirectConversion()
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
            EditorGUILayout.LabelField("Apex Physical Humanoid Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Converts a valid Humanoid Avatar into a visible physical skeleton driven by a hidden animated target. Starter colliders and joint limits are generated automatically and remain editable.",
                MessageType.Info);

            EditorGUILayout.Space();
            character = (GameObject)EditorGUILayout.ObjectField(
                "Humanoid Character",
                character,
                typeof(GameObject),
                true);
            mode = (ApexPhysicalHumanoidMode)EditorGUILayout.EnumPopup("Conversion Mode", mode);

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
            DrawModeDescription();
            EditorGUILayout.Space();

            bool valid = ApexHumanoidConverter.CanConvert(character, out string reason);
            EditorGUILayout.HelpBox(
                valid ? "Humanoid validation passed." : reason,
                valid ? MessageType.Info : MessageType.Warning);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Convert Humanoid", GUILayout.Height(38f)))
                {
                    ApexPhysicalHumanoid converted = ApexHumanoidConverter.Convert(
                        character,
                        mode,
                        saveAsPrefab,
                        createSpawnableCrate);
                    if (converted != null)
                    {
                        character = converted.gameObject;
                    }
                }
            }
        }

        private void DrawModeDescription()
        {
            switch (mode)
            {
                case ApexPhysicalHumanoidMode.PhysicalPlayer:
                    EditorGUILayout.HelpBox(
                        "Adds the full-body hips motor and generated head/hand tracking targets. External OpenXR or Input System adapters drive those targets and the motor API.",
                        MessageType.None);
                    break;
                case ApexPhysicalHumanoidMode.PhysicalNPC:
                    EditorGUILayout.HelpBox(
                        "Adds NavMesh path planning, force-driven NPC movement, balance, wandering, chasing, and impact-triggered behavior to the physical hips.",
                        MessageType.None);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "Builds the active physical body and animated target without adding player or NPC locomotion.",
                        MessageType.None);
                    break;
            }
        }

        private static void ConvertSelection(ApexPhysicalHumanoidMode conversionMode)
        {
            GameObject selected = Selection.activeObject as GameObject;
            ApexHumanoidConverter.Convert(selected, conversionMode, true, false);
        }
    }
}

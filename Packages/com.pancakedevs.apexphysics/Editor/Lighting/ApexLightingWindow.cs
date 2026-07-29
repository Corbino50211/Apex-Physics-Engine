using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal sealed class ApexLightingWindow : EditorWindow
    {
        private Vector2 scrollPosition;

        public static void Open()
        {
            ApexLightingWindow window = GetWindow<ApexLightingWindow>("Apex Lighting Baker");
            window.minSize = new Vector2(440f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            ApexLightingBaker.StateChanged += Repaint;
            EditorApplication.update += RepaintWhileBaking;
        }

        private void OnDisable()
        {
            ApexLightingBaker.StateChanged -= Repaint;
            EditorApplication.update -= RepaintWhileBaking;
        }

        private void OnGUI()
        {
            ApexLightingProjectSettings settings = ApexLightingProjectSettings.instance;
            Scene scene = ApexLightingBaker.ActiveScene;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Apex Physics Engine", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Automatic Lighting Baker", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            DrawSceneStatus(scene);
            EditorGUILayout.Space();
            DrawAutomationSettings(settings);
            EditorGUILayout.Space();
            DrawQualitySettings(settings);
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();

            MessageType messageType = ApexLightingBaker.IsBaking
                ? MessageType.Info
                : MessageType.None;
            EditorGUILayout.HelpBox(ApexLightingBaker.StatusMessage, messageType);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSceneStatus(Scene scene)
        {
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Scene", scene.IsValid() ? scene.name : "None");
            EditorGUILayout.LabelField("Lighting Data", ApexLightingBaker.HasLightingData ? "Present" : "Missing");

            string dirtyState = ApexLightingBaker.IsBaking
                ? "Baking"
                : ApexLightingBaker.IsDirty ? "Dirty" : "Current";
            EditorGUILayout.LabelField("Bake State", dirtyState);

            DateTime? lastBakeUtc = ApexLightingBaker.LastBakeUtc;
            EditorGUILayout.LabelField(
                "Last Apex Bake",
                lastBakeUtc.HasValue ? lastBakeUtc.Value.ToLocalTime().ToString("g") : "Never");

            if (ApexLightingBaker.IsBaking)
            {
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(
                    progressRect,
                    Mathf.Clamp01(ApexLightingBaker.Progress),
                    $"Baking {ApexLightingBaker.Progress * 100f:0}%");
            }
        }

        private static void DrawAutomationSettings(ApexLightingProjectSettings settings)
        {
            EditorGUILayout.LabelField("Play Mode Automation", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            settings.bakeBeforePlay = EditorGUILayout.Toggle(
                new GUIContent("Bake Before Play", "Intercept Play Mode and bake lighting first."),
                settings.bakeBeforePlay);
            settings.bakeOnlyWhenDirty = EditorGUILayout.Toggle(
                new GUIContent("Only When Dirty", "Skip the bake when Apex detects no relevant scene changes."),
                settings.bakeOnlyWhenDirty);
            settings.enterPlayAfterBake = EditorGUILayout.Toggle(
                new GUIContent("Enter Play After Bake", "Automatically resume Play Mode when the bake finishes."),
                settings.enterPlayAfterBake);
            settings.bakeReflectionProbes = EditorGUILayout.Toggle(
                new GUIContent("Bake Reflection Probes", "Bake enabled Baked-mode reflection probes after lightmaps."),
                settings.bakeReflectionProbes);

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }

        private static void DrawQualitySettings(ApexLightingProjectSettings settings)
        {
            EditorGUILayout.LabelField("Bake Quality", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            settings.preset = (ApexLightingBakePreset)EditorGUILayout.EnumPopup("Preset", settings.preset);

            switch (settings.preset)
            {
                case ApexLightingBakePreset.Preview:
                    EditorGUILayout.HelpBox(
                        "Fast iteration preset: 10 texels/unit, 16 direct samples, 64 indirect samples, and 1 bounce.",
                        MessageType.Info);
                    break;

                case ApexLightingBakePreset.Production:
                    EditorGUILayout.HelpBox(
                        "Higher-quality preset: 40 texels/unit, 64 direct samples, 512 indirect samples, and 4 bounces.",
                        MessageType.Info);
                    break;

                case ApexLightingBakePreset.Custom:
                    settings.customLightmapResolution = EditorGUILayout.FloatField(
                        "Lightmap Resolution",
                        settings.customLightmapResolution);
                    settings.customDirectSamples = EditorGUILayout.IntField(
                        "Direct Samples",
                        settings.customDirectSamples);
                    settings.customIndirectSamples = EditorGUILayout.IntField(
                        "Indirect Samples",
                        settings.customIndirectSamples);
                    settings.customEnvironmentSamples = EditorGUILayout.IntField(
                        "Environment Samples",
                        settings.customEnvironmentSamples);
                    settings.customMaxBounces = EditorGUILayout.IntField(
                        "Maximum Bounces",
                        settings.customMaxBounces);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
                ApexLightingBaker.MarkDirty();
            }

            if (GUILayout.Button("Apply Selected Preset to Scene"))
            {
                ApexLightingBaker.ApplySelectedPreset();
            }
        }

        private static void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(ApexLightingBaker.IsBaking))
            {
                if (GUILayout.Button("Bake and Enter Play Mode", GUILayout.Height(30f)))
                {
                    ApexLightingBaker.BakeAndPlay();
                }

                if (GUILayout.Button("Bake Lighting Now"))
                {
                    ApexLightingBaker.BakeNow(false, true);
                }

                if (GUILayout.Button("Bake Reflection Probes"))
                {
                    ApexLightingBaker.BakeReflectionProbes();
                }

                if (GUILayout.Button("Mark Lighting Dirty"))
                {
                    ApexLightingBaker.MarkDirty();
                }

                if (GUILayout.Button("Clear Lighting Data"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Clear Apex Lighting Data",
                        "Clear the loaded scenes' lighting data and mark the active scene dirty?",
                        "Clear",
                        "Cancel");
                    if (confirmed)
                    {
                        ApexLightingBaker.ClearLightingData();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!ApexLightingBaker.IsBaking))
            {
                if (GUILayout.Button("Cancel Active Bake"))
                {
                    ApexLightingBaker.CancelBake();
                }
            }
        }

        private void RepaintWhileBaking()
        {
            if (ApexLightingBaker.IsBaking)
            {
                Repaint();
            }
        }
    }
}

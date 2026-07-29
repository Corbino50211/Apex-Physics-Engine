using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics.Editor
{
    [InitializeOnLoad]
    internal static class ApexLightingBaker
    {
        private static bool bakeStartedByApex;
        private static bool pendingEnterPlay;
        private static bool resumePlayWithoutBake;
        private static bool playBakeQueued;
        private static string statusMessage = "Ready";

        public static event Action StateChanged;

        static ApexLightingBaker()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            Lightmapping.bakeCompleted += HandleBakeCompleted;
        }

        public static bool IsBaking => Lightmapping.isRunning;
        public static float Progress => Lightmapping.isRunning ? Lightmapping.buildProgress : 0f;
        public static string StatusMessage => statusMessage;

        public static Scene ActiveScene => SceneManager.GetActiveScene();
        public static string ActiveSceneKey => ApexLightingHasher.GetSceneKey(ActiveScene);

        public static bool HasLightingData
        {
            get
            {
                Scene scene = ActiveScene;
                return scene.IsValid() && Lightmapping.GetLightingDataAssetForScene(scene) != null;
            }
        }

        public static bool IsDirty
        {
            get
            {
                Scene scene = ActiveScene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    return true;
                }

                if (!HasLightingData)
                {
                    return true;
                }

                string currentHash = ApexLightingHasher.Compute(scene);
                string storedHash = ApexLightingProjectSettings.instance.GetStoredHash(
                    ApexLightingHasher.GetSceneKey(scene));
                return string.IsNullOrEmpty(storedHash) ||
                       !string.Equals(currentHash, storedHash, StringComparison.Ordinal);
            }
        }

        public static DateTime? LastBakeUtc =>
            ApexLightingProjectSettings.instance.GetLastBakeUtc(ActiveSceneKey);

        public static void OpenWindow()
        {
            ApexLightingWindow.Open();
        }

        public static void BakeAndPlay()
        {
            ApexLightingProjectSettings settings = ApexLightingProjectSettings.instance;
            if (settings.bakeOnlyWhenDirty && !IsDirty)
            {
                statusMessage = "Lighting is current. Entering Play Mode without baking.";
                resumePlayWithoutBake = true;
                StateChanged?.Invoke();
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
                return;
            }

            BakeNow(true, true);
        }

        public static bool BakeNow(bool enterPlayWhenFinished = false, bool force = true)
        {
            if (Lightmapping.isRunning)
            {
                statusMessage = "A lighting bake is already running.";
                StateChanged?.Invoke();
                return false;
            }

            Scene scene = ActiveScene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                statusMessage = "No loaded active scene is available to bake.";
                StateChanged?.Invoke();
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                statusMessage = "Lighting bake canceled because the scene was not saved.";
                StateChanged?.Invoke();
                return false;
            }

            if (!force && ApexLightingProjectSettings.instance.bakeOnlyWhenDirty && !IsDirty)
            {
                statusMessage = "Lighting is already current.";
                StateChanged?.Invoke();
                return false;
            }

            ApplySelectedPreset();

            pendingEnterPlay = enterPlayWhenFinished;
            bakeStartedByApex = true;
            statusMessage = $"Baking lighting for {scene.name}...";
            StateChanged?.Invoke();

            bool started = Lightmapping.BakeAsync();
            if (!started)
            {
                bakeStartedByApex = false;
                pendingEnterPlay = false;
                statusMessage = "Unity could not start the lighting bake. Check the Console and Lighting Settings.";
                StateChanged?.Invoke();
                return false;
            }

            return true;
        }

        public static void CancelBake()
        {
            if (!Lightmapping.isRunning)
            {
                statusMessage = "No lighting bake is currently running.";
                StateChanged?.Invoke();
                return;
            }

            bakeStartedByApex = false;
            pendingEnterPlay = false;
            Lightmapping.Cancel();
            statusMessage = "Lighting bake canceled.";
            StateChanged?.Invoke();
        }

        public static void ClearLightingData()
        {
            if (Lightmapping.isRunning)
            {
                CancelBake();
            }

            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            MarkDirty();
            AssetDatabase.Refresh();
            statusMessage = "Cleared lighting data for the loaded scenes.";
            StateChanged?.Invoke();
        }

        public static void MarkDirty()
        {
            string sceneKey = ActiveSceneKey;
            ApexLightingProjectSettings.instance.MarkDirty(sceneKey);
            statusMessage = "Active scene lighting marked dirty.";
            StateChanged?.Invoke();
        }

        public static int BakeReflectionProbes()
        {
            if (Lightmapping.isRunning)
            {
                statusMessage = "Wait for the active lightmap bake before baking reflection probes.";
                StateChanged?.Invoke();
                return 0;
            }

            int count = BakeReflectionProbesInternal(ActiveScene);
            statusMessage = count > 0
                ? $"Baked {count} reflection probe(s)."
                : "No enabled baked reflection probes were found in the active scene.";
            StateChanged?.Invoke();
            return count;
        }

        public static void ApplySelectedPreset()
        {
            Scene scene = ActiveScene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            LightingSettings lightingSettings = Lightmapping.GetLightingSettingsForScene(scene);
            if (lightingSettings == null)
            {
                lightingSettings = new LightingSettings
                {
                    name = "Apex Generated Lighting Settings"
                };
                Lightmapping.SetLightingSettingsForScene(scene, lightingSettings);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            ApexLightingProjectSettings projectSettings = ApexLightingProjectSettings.instance;
            lightingSettings.autoGenerate = false;
            lightingSettings.bakedGI = true;

            switch (projectSettings.preset)
            {
                case ApexLightingBakePreset.Preview:
                    ApplyQuality(
                        lightingSettings,
                        projectSettings.previewLightmapResolution,
                        projectSettings.previewDirectSamples,
                        projectSettings.previewIndirectSamples,
                        projectSettings.previewEnvironmentSamples,
                        projectSettings.previewMaxBounces);
                    break;

                case ApexLightingBakePreset.Production:
                    ApplyQuality(
                        lightingSettings,
                        projectSettings.productionLightmapResolution,
                        projectSettings.productionDirectSamples,
                        projectSettings.productionIndirectSamples,
                        projectSettings.productionEnvironmentSamples,
                        projectSettings.productionMaxBounces);
                    break;

                case ApexLightingBakePreset.Custom:
                    ApplyQuality(
                        lightingSettings,
                        projectSettings.customLightmapResolution,
                        projectSettings.customDirectSamples,
                        projectSettings.customIndirectSamples,
                        projectSettings.customEnvironmentSamples,
                        projectSettings.customMaxBounces);
                    break;
            }

            EditorUtility.SetDirty(lightingSettings);
            projectSettings.SaveSettings();
            statusMessage = $"Applied {projectSettings.preset} lighting preset.";
            StateChanged?.Invoke();
        }

        private static void ApplyQuality(
            LightingSettings settings,
            float resolution,
            int directSamples,
            int indirectSamples,
            int environmentSamples,
            int maxBounces)
        {
            settings.lightmapResolution = Mathf.Max(0.0001f, resolution);
            settings.directSampleCount = Mathf.Max(1, directSamples);
            settings.indirectSampleCount = Mathf.Max(8, indirectSamples);
            settings.environmentSampleCount = Mathf.Max(8, environmentSamples);
            settings.maxBounces = Mathf.Max(0, maxBounces);
            settings.minBounces = Mathf.Min(settings.minBounces, settings.maxBounces);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (resumePlayWithoutBake)
            {
                resumePlayWithoutBake = false;
                return;
            }

            ApexLightingProjectSettings settings = ApexLightingProjectSettings.instance;
            if (!settings.bakeBeforePlay)
            {
                return;
            }

            bool shouldBake = !settings.bakeOnlyWhenDirty || IsDirty;
            if (!shouldBake)
            {
                return;
            }

            EditorApplication.isPlaying = false;
            if (playBakeQueued)
            {
                return;
            }

            playBakeQueued = true;
            statusMessage = "Play Mode paused while Apex prepares the lighting bake.";
            StateChanged?.Invoke();

            EditorApplication.delayCall += () =>
            {
                playBakeQueued = false;
                BakeNow(true, true);
            };
        }

        private static void HandleBakeCompleted()
        {
            if (!bakeStartedByApex)
            {
                return;
            }

            bakeStartedByApex = false;
            Scene scene = ActiveScene;
            ApexLightingProjectSettings settings = ApexLightingProjectSettings.instance;

            int reflectionProbeCount = 0;
            if (settings.bakeReflectionProbes)
            {
                reflectionProbeCount = BakeReflectionProbesInternal(scene);
            }

            string currentHash = ApexLightingHasher.Compute(scene);
            settings.StoreBake(ApexLightingHasher.GetSceneKey(scene), currentHash);
            AssetDatabase.SaveAssets();

            bool shouldEnterPlay = pendingEnterPlay && settings.enterPlayAfterBake;
            pendingEnterPlay = false;
            statusMessage = reflectionProbeCount > 0
                ? $"Lighting bake completed with {reflectionProbeCount} reflection probe(s)."
                : "Lighting bake completed.";
            StateChanged?.Invoke();

            if (shouldEnterPlay)
            {
                resumePlayWithoutBake = true;
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            }
        }

        private static int BakeReflectionProbesInternal(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            {
                return 0;
            }

            ReflectionProbe[] probes = Resources.FindObjectsOfTypeAll<ReflectionProbe>();
            string sceneDirectory = Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
            string sceneName = Path.GetFileNameWithoutExtension(scene.path);
            if (string.IsNullOrEmpty(sceneDirectory) || string.IsNullOrEmpty(sceneName))
            {
                return 0;
            }

            string outputFolder = $"{sceneDirectory}/{sceneName}_ApexLighting";
            EnsureAssetFolder(outputFolder);

            int bakedCount = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                ReflectionProbe probe = probes[i];
                if (probe == null ||
                    EditorUtility.IsPersistent(probe) ||
                    probe.gameObject.scene != scene ||
                    !probe.enabled ||
                    probe.mode != ReflectionProbeMode.Baked)
                {
                    continue;
                }

                string stableId = GlobalObjectId.GetGlobalObjectIdSlow(probe).ToString();
                string fileName = $"{SanitizeFileName(probe.name)}_{SanitizeFileName(stableId)}.exr";
                string outputPath = $"{outputFolder}/{fileName}";
                if (Lightmapping.BakeReflectionProbe(probe, outputPath))
                {
                    bakedCount++;
                }
            }

            AssetDatabase.Refresh();
            return bakedCount;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Probe";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                value = value.Replace(invalidCharacters[i], '_');
            }

            return value.Replace(':', '_').Replace('-', '_').Replace(' ', '_');
        }
    }
}

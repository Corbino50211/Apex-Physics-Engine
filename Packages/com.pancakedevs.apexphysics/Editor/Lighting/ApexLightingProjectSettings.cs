using System;
using System.Collections.Generic;
using UnityEditor;

namespace PancakeDevs.ApexPhysics.Editor
{
    [Serializable]
    internal sealed class ApexLightingSceneRecord
    {
        public string sceneKey;
        public string lightingHash;
        public long lastBakeUtcTicks;
    }

    [FilePath("ProjectSettings/ApexPhysicsLighting.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ApexLightingProjectSettings : ScriptableSingleton<ApexLightingProjectSettings>
    {
        public bool bakeBeforePlay = true;
        public bool bakeOnlyWhenDirty = true;
        public bool enterPlayAfterBake = true;
        public bool bakeReflectionProbes = true;
        public ApexLightingBakePreset preset = ApexLightingBakePreset.Preview;

        public float customLightmapResolution = 20f;
        public int customDirectSamples = 32;
        public int customIndirectSamples = 256;
        public int customEnvironmentSamples = 128;
        public int customMaxBounces = 2;

        public float previewLightmapResolution = 10f;
        public int previewDirectSamples = 16;
        public int previewIndirectSamples = 64;
        public int previewEnvironmentSamples = 32;
        public int previewMaxBounces = 1;

        public float productionLightmapResolution = 40f;
        public int productionDirectSamples = 64;
        public int productionIndirectSamples = 512;
        public int productionEnvironmentSamples = 256;
        public int productionMaxBounces = 4;

        public List<ApexLightingSceneRecord> sceneRecords = new List<ApexLightingSceneRecord>();

        public string GetStoredHash(string sceneKey)
        {
            ApexLightingSceneRecord record = FindRecord(sceneKey);
            return record != null ? record.lightingHash : string.Empty;
        }

        public DateTime? GetLastBakeUtc(string sceneKey)
        {
            ApexLightingSceneRecord record = FindRecord(sceneKey);
            if (record == null || record.lastBakeUtcTicks <= 0)
            {
                return null;
            }

            return new DateTime(record.lastBakeUtcTicks, DateTimeKind.Utc);
        }

        public void StoreBake(string sceneKey, string lightingHash)
        {
            ApexLightingSceneRecord record = FindRecord(sceneKey);
            if (record == null)
            {
                record = new ApexLightingSceneRecord { sceneKey = sceneKey };
                sceneRecords.Add(record);
            }

            record.lightingHash = lightingHash ?? string.Empty;
            record.lastBakeUtcTicks = DateTime.UtcNow.Ticks;
            Save(true);
        }

        public void MarkDirty(string sceneKey)
        {
            ApexLightingSceneRecord record = FindRecord(sceneKey);
            if (record == null)
            {
                return;
            }

            record.lightingHash = string.Empty;
            Save(true);
        }

        public void SaveSettings()
        {
            customLightmapResolution = Math.Max(0.0001f, customLightmapResolution);
            customDirectSamples = Math.Max(1, customDirectSamples);
            customIndirectSamples = Math.Max(8, customIndirectSamples);
            customEnvironmentSamples = Math.Max(8, customEnvironmentSamples);
            customMaxBounces = Math.Max(0, customMaxBounces);
            Save(true);
        }

        private ApexLightingSceneRecord FindRecord(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
            {
                return null;
            }

            for (int i = 0; i < sceneRecords.Count; i++)
            {
                ApexLightingSceneRecord record = sceneRecords[i];
                if (record != null && string.Equals(record.sceneKey, sceneKey, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }
    }
}

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexLightingHasher
    {
        public static string GetSceneKey(Scene scene)
        {
            if (!scene.IsValid())
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(scene.path)
                ? $"Unsaved:{scene.name}"
                : scene.path;
        }

        public static string Compute(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(8192);
            builder.Append("Scene|").Append(GetSceneKey(scene));
            AppendRenderSettings(builder);
            AppendLightingSettings(builder, scene);
            AppendLights(builder, scene);
            AppendRenderers(builder, scene);
            AppendTerrains(builder, scene);
            AppendReflectionProbes(builder, scene);
            return Hash128.Compute(builder.ToString()).ToString();
        }

        private static void AppendRenderSettings(StringBuilder builder)
        {
            builder.Append("|RenderSettings|")
                .Append(RenderSettings.ambientMode).Append('|')
                .Append(RenderSettings.ambientIntensity).Append('|')
                .Append(RenderSettings.ambientLight).Append('|')
                .Append(RenderSettings.ambientSkyColor).Append('|')
                .Append(RenderSettings.ambientEquatorColor).Append('|')
                .Append(RenderSettings.ambientGroundColor).Append('|')
                .Append(RenderSettings.reflectionIntensity).Append('|')
                .Append(RenderSettings.reflectionBounces).Append('|')
                .Append(RenderSettings.defaultReflectionMode).Append('|')
                .Append(RenderSettings.defaultReflectionResolution).Append('|')
                .Append(RenderSettings.fog).Append('|')
                .Append(RenderSettings.fogColor).Append('|')
                .Append(RenderSettings.fogMode).Append('|')
                .Append(RenderSettings.fogDensity).Append('|')
                .Append(RenderSettings.fogStartDistance).Append('|')
                .Append(RenderSettings.fogEndDistance).Append('|');

            AppendAsset(builder, RenderSettings.skybox);
            AppendObjectId(builder, RenderSettings.sun);
        }

        private static void AppendLightingSettings(StringBuilder builder, Scene scene)
        {
            LightingSettings settings = Lightmapping.GetLightingSettingsForScene(scene);
            if (settings == null)
            {
                builder.Append("|LightingSettings:null|");
                return;
            }

            builder.Append("|LightingSettings|")
                .Append(settings.bakedGI).Append('|')
                .Append(settings.realtimeGI).Append('|')
                .Append(settings.lightmapper).Append('|')
                .Append(settings.lightmapResolution).Append('|')
                .Append(settings.lightmapMaxSize).Append('|')
                .Append(settings.lightmapPadding).Append('|')
                .Append(settings.directSampleCount).Append('|')
                .Append(settings.indirectSampleCount).Append('|')
                .Append(settings.environmentSampleCount).Append('|')
                .Append(settings.minBounces).Append('|')
                .Append(settings.maxBounces).Append('|')
                .Append(settings.mixedBakeMode).Append('|')
                .Append(settings.directionalityMode).Append('|');
        }

        private static void AppendLights(StringBuilder builder, Scene scene)
        {
            Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
            Array.Sort(lights, CompareObjects);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (!BelongsToScene(light, scene))
                {
                    continue;
                }

                builder.Append("|Light|");
                AppendObjectId(builder, light);
                AppendTransform(builder, light.transform);
                builder.Append(light.enabled).Append('|')
                    .Append(light.type).Append('|')
                    .Append(light.lightmapBakeType).Append('|')
                    .Append(light.color).Append('|')
                    .Append(light.colorTemperature).Append('|')
                    .Append(light.useColorTemperature).Append('|')
                    .Append(light.intensity).Append('|')
                    .Append(light.bounceIntensity).Append('|')
                    .Append(light.range).Append('|')
                    .Append(light.spotAngle).Append('|')
                    .Append(light.innerSpotAngle).Append('|')
                    .Append(light.shadows).Append('|')
                    .Append(light.shadowStrength).Append('|')
                    .Append(light.shadowBias).Append('|')
                    .Append(light.shadowNormalBias).Append('|')
                    .Append(light.cullingMask).Append('|');
            }
        }

        private static void AppendRenderers(StringBuilder builder, Scene scene)
        {
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            Array.Sort(renderers, CompareObjects);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!BelongsToScene(renderer, scene))
                {
                    continue;
                }

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0)
                {
                    continue;
                }

                builder.Append("|Renderer|");
                AppendObjectId(builder, renderer);
                AppendTransform(builder, renderer.transform);
                builder.Append(renderer.enabled).Append('|')
                    .Append(flags).Append('|')
                    .Append(renderer.shadowCastingMode).Append('|')
                    .Append(renderer.receiveShadows).Append('|');

                if (renderer is MeshRenderer meshRenderer)
                {
                    builder.Append(meshRenderer.scaleInLightmap).Append('|');
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    AppendAsset(builder, meshFilter != null ? meshFilter.sharedMesh : null);
                }
                else if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    AppendAsset(builder, skinnedRenderer.sharedMesh);
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    AppendAsset(builder, materials[materialIndex]);
                }
            }
        }

        private static void AppendTerrains(StringBuilder builder, Scene scene)
        {
            Terrain[] terrains = Resources.FindObjectsOfTypeAll<Terrain>();
            Array.Sort(terrains, CompareObjects);

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (!BelongsToScene(terrain, scene))
                {
                    continue;
                }

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(terrain.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0)
                {
                    continue;
                }

                builder.Append("|Terrain|");
                AppendObjectId(builder, terrain);
                AppendTransform(builder, terrain.transform);
                builder.Append(terrain.enabled).Append('|')
                    .Append(flags).Append('|')
                    .Append(terrain.lightmapScaleOffset).Append('|');
                AppendAsset(builder, terrain.terrainData);
                AppendAsset(builder, terrain.materialTemplate);
            }
        }

        private static void AppendReflectionProbes(StringBuilder builder, Scene scene)
        {
            ReflectionProbe[] probes = Resources.FindObjectsOfTypeAll<ReflectionProbe>();
            Array.Sort(probes, CompareObjects);

            for (int i = 0; i < probes.Length; i++)
            {
                ReflectionProbe probe = probes[i];
                if (!BelongsToScene(probe, scene))
                {
                    continue;
                }

                builder.Append("|ReflectionProbe|");
                AppendObjectId(builder, probe);
                AppendTransform(builder, probe.transform);
                builder.Append(probe.enabled).Append('|')
                    .Append(probe.mode).Append('|')
                    .Append(probe.center).Append('|')
                    .Append(probe.size).Append('|')
                    .Append(probe.resolution).Append('|')
                    .Append(probe.intensity).Append('|')
                    .Append(probe.importance).Append('|')
                    .Append(probe.blendDistance).Append('|')
                    .Append(probe.boxProjection).Append('|')
                    .Append(probe.cullingMask).Append('|')
                    .Append(probe.nearClipPlane).Append('|')
                    .Append(probe.farClipPlane).Append('|')
                    .Append(probe.hdr).Append('|');
            }
        }

        private static bool BelongsToScene(Component component, Scene scene)
        {
            return component != null &&
                   !EditorUtility.IsPersistent(component) &&
                   component.gameObject.scene == scene;
        }

        private static int CompareObjects(Component left, Component right)
        {
            string leftId = left != null ? GlobalObjectId.GetGlobalObjectIdSlow(left).ToString() : string.Empty;
            string rightId = right != null ? GlobalObjectId.GetGlobalObjectIdSlow(right).ToString() : string.Empty;
            return string.CompareOrdinal(leftId, rightId);
        }

        private static void AppendObjectId(StringBuilder builder, UnityEngine.Object target)
        {
            if (target == null)
            {
                builder.Append("null|");
                return;
            }

            builder.Append(GlobalObjectId.GetGlobalObjectIdSlow(target)).Append('|');
        }

        private static void AppendTransform(StringBuilder builder, Transform target)
        {
            builder.Append(target.position).Append('|')
                .Append(target.rotation).Append('|')
                .Append(target.lossyScale).Append('|');
        }

        private static void AppendAsset(StringBuilder builder, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                builder.Append("null|");
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            builder.Append(string.IsNullOrEmpty(path) ? asset.GetInstanceID().ToString() : AssetDatabase.AssetPathToGUID(path))
                .Append('|');
        }
    }
}

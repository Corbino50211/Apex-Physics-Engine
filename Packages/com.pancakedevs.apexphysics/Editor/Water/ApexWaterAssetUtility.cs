using System;
using System.IO;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexWaterAssetUtility
    {
        private const string GeneratedRoot = "Assets/Apex Generated";
        private const string WaterRoot = GeneratedRoot + "/Water";
        private const string SimpleShaderName = "Apex Physics Engine/Water/Simple";
        private const string UrpShaderName = "Apex Physics Engine/Water/URP Low Poly";

        internal static bool GenerateSurfaceAssets(
            ApexWaterVolume volume,
            bool preferUrpShader,
            out string message)
        {
            if (volume == null)
            {
                message = "No Apex water volume was supplied.";
                return false;
            }

            EnsureFolder(GeneratedRoot);
            EnsureFolder(WaterRoot);
            string objectFolder = WaterRoot + "/" + SanitizeFileName(volume.name);
            EnsureFolder(objectFolder);

            try
            {
                Mesh mesh = volume.CreateSurfaceMesh();
                string meshPath = objectFolder + "/" +
                                  SanitizeFileName(volume.name) + "_Surface.asset";
                ReplaceAsset(mesh, meshPath);

                string materialPath = volume.GeneratedMaterialPath;
                Material material = volume.WaterMaterial;
                if (material == null)
                {
                    Shader shader = preferUrpShader ? Shader.Find(UrpShaderName) : null;
                    if (shader == null)
                    {
                        shader = Shader.Find(SimpleShaderName);
                    }
                    if (shader == null)
                    {
                        message = "Apex could not find its water shader.";
                        return false;
                    }

                    material = new Material(shader)
                    {
                        name = SanitizeFileName(volume.name) + "_Water"
                    };
                    materialPath = objectFolder + "/" + material.name + ".mat";
                    ReplaceAsset(material, materialPath);
                }

                volume.AssignSurfaceMesh(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath));
                volume.AssignWaterMaterial(material);
                volume.EditorSetGeneratedAssetPaths(meshPath, materialPath);
                EditorUtility.SetDirty(volume);
                EditorUtility.SetDirty(volume.gameObject);
                if (volume.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        volume.gameObject.scene);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                message = "Generated Apex water surface assets for " + volume.name + ".";
                return true;
            }
            catch (Exception exception)
            {
                message = "Apex water generation failed: " + exception.Message;
                return false;
            }
        }

        internal static void ClearGeneratedSurface(ApexWaterVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            MeshFilter filter = volume.GetComponent<MeshFilter>();
            if (filter != null && !string.IsNullOrWhiteSpace(volume.GeneratedSurfaceMeshPath) &&
                AssetDatabase.GetAssetPath(filter.sharedMesh) == volume.GeneratedSurfaceMeshPath)
            {
                filter.sharedMesh = null;
            }

            MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
            if (renderer != null && !string.IsNullOrWhiteSpace(volume.GeneratedMaterialPath) &&
                AssetDatabase.GetAssetPath(renderer.sharedMaterial) == volume.GeneratedMaterialPath)
            {
                renderer.sharedMaterial = null;
                volume.AssignWaterMaterial(null);
            }

            DeleteAssetIfGenerated(volume.GeneratedSurfaceMeshPath);
            DeleteAssetIfGenerated(volume.GeneratedMaterialPath);
            volume.EditorSetGeneratedAssetPaths(string.Empty, string.Empty);
            EditorUtility.SetDirty(volume);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static bool HasUrpSampleShader()
        {
            return Shader.Find(UrpShaderName) != null;
        }

        private static void ReplaceAsset(UnityEngine.Object asset, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void DeleteAssetIfGenerated(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                path.StartsWith(WaterRoot + "/", StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Water" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                result = result.Replace(invalid[i], '_');
            }
            return result.Replace('/', '_').Replace('\\', '_').Trim();
        }
    }
}

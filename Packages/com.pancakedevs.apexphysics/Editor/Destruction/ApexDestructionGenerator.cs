using System;
using System.Collections.Generic;
using System.IO;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexDestructionGenerator
    {
        private const string GeneratedRootName = "Apex Fracture Chunks";
        private const string GeneratedAssetsRoot = "Assets/Apex Generated/Destruction";

        public static bool Generate(
            GameObject target,
            int chunkCount,
            int seed,
            float minimumChunkVolume,
            float planeJitter,
            float capUvScale,
            Material interiorMaterial,
            out string message)
        {
            message = string.Empty;
            if (!TryResolveSource(target, out MeshFilter sourceFilter, out MeshRenderer sourceRenderer, out message))
            {
                return false;
            }

            ApexDestructible destructible = target.GetComponent<ApexDestructible>();
            if (destructible != null && destructible.HasGeneratedFracture)
            {
                ClearGeneratedFracture(destructible, true);
            }

            ApexFractureOptions options = new ApexFractureOptions(
                chunkCount,
                seed,
                minimumChunkVolume,
                planeJitter,
                capUvScale);

            if (!ApexMeshFracturer.TryFracture(
                    sourceFilter.sharedMesh,
                    options,
                    out List<Mesh> fracturedMeshes,
                    out message))
            {
                return false;
            }

            if (fracturedMeshes.Count < 2)
            {
                DestroyTemporaryMeshes(fracturedMeshes);
                message = "Apex produced fewer than two usable fracture chunks.";
                return false;
            }

            string assetFolder = CreateGeneratedAssetFolder(target.name);
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                DestroyTemporaryMeshes(fracturedMeshes);
                message = "Apex could not create the generated destruction asset folder.";
                return false;
            }

            try
            {
                Collider sourceCollider = target.GetComponent<Collider>();
                if (sourceCollider == null)
                {
                    MeshCollider generatedIntactCollider = Undo.AddComponent<MeshCollider>(target);
                    generatedIntactCollider.sharedMesh = sourceFilter.sharedMesh;
                    generatedIntactCollider.convex = true;
                    sourceCollider = generatedIntactCollider;
                }

                Rigidbody sourceBody = target.GetComponent<Rigidbody>();
                if (sourceBody == null)
                {
                    sourceBody = Undo.AddComponent<Rigidbody>(target);
                    sourceBody.isKinematic = true;
                    sourceBody.useGravity = false;
                    sourceBody.interpolation = RigidbodyInterpolation.Interpolate;
                }
                sourceBody.detectCollisions = true;

                ApexBody apexBody = target.GetComponent<ApexBody>();
                if (apexBody == null)
                {
                    apexBody = Undo.AddComponent<ApexBody>(target);
                }

                if (destructible == null)
                {
                    destructible = Undo.AddComponent<ApexDestructible>(target);
                }

                Renderer[] intactRenderers = target.GetComponentsInChildren<Renderer>(true);
                Collider[] intactColliders = target.GetComponentsInChildren<Collider>(true);

                GameObject rootObject = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Generate Apex Fracture");
                Transform root = rootObject.transform;
                root.SetParent(target.transform, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;

                Material[] chunkMaterials = BuildChunkMaterials(
                    sourceRenderer.sharedMaterials,
                    sourceFilter.sharedMesh.subMeshCount,
                    interiorMaterial);

                float totalVolume = 0f;
                for (int i = 0; i < fracturedMeshes.Count; i++)
                {
                    totalVolume += Mathf.Max(0.000001f, BoundsVolume(fracturedMeshes[i].bounds));
                }

                List<ApexDestructibleChunk> chunks = new List<ApexDestructibleChunk>();
                for (int i = 0; i < fracturedMeshes.Count; i++)
                {
                    Mesh chunkMesh = fracturedMeshes[i];
                    Vector3 localCenter = RecenterMesh(chunkMesh);
                    string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                        assetFolder + "/" + SanitizeFileName(target.name) +
                        "_Chunk_" + (i + 1).ToString("00") + ".asset");
                    AssetDatabase.CreateAsset(chunkMesh, meshPath);

                    GameObject chunkObject = new GameObject("Chunk " + (i + 1).ToString("00"));
                    Undo.RegisterCreatedObjectUndo(chunkObject, "Generate Apex Fracture Chunk");
                    Transform chunkTransform = chunkObject.transform;
                    chunkTransform.SetParent(root, false);
                    chunkTransform.localPosition = localCenter;
                    chunkTransform.localRotation = Quaternion.identity;
                    chunkTransform.localScale = Vector3.one;

                    MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = chunkMesh;

                    MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterials = chunkMaterials;
                    CopyRendererSettings(sourceRenderer, meshRenderer);

                    MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = chunkMesh;
                    meshCollider.convex = true;
                    meshCollider.enabled = false;

                    Rigidbody chunkBody = chunkObject.AddComponent<Rigidbody>();
                    ConfigureChunkRigidbody(
                        sourceBody,
                        chunkBody,
                        chunkMesh,
                        totalVolume);

                    ApexDestructibleChunk chunk = chunkObject.AddComponent<ApexDestructibleChunk>();
                    chunk.EditorConfigureGeneratedChunk(destructible);
                    meshRenderer.enabled = false;
                    chunkBody.isKinematic = true;
                    chunkBody.useGravity = false;
                    chunkBody.detectCollisions = false;
                    chunks.Add(chunk);
                }

                destructible.EditorAssignGeneratedFracture(
                    root,
                    chunks.ToArray(),
                    intactRenderers,
                    intactColliders,
                    assetFolder);

                EditorUtility.SetDirty(destructible);
                EditorUtility.SetDirty(sourceBody);
                EditorUtility.SetDirty(sourceCollider);
                EditorSceneManager.MarkSceneDirty(target.scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeGameObject = target;
                message =
                    $"Generated {chunks.Count} Apex fracture chunks for {target.name}.";
                return true;
            }
            catch (Exception exception)
            {
                AssetDatabase.DeleteAsset(assetFolder);
                DestroyTemporaryMeshes(fracturedMeshes);
                message = "Apex fracture generation failed: " + exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        public static void ClearGeneratedFracture(
            ApexDestructible destructible,
            bool deleteGeneratedAssets)
        {
            if (destructible == null)
            {
                return;
            }

            Transform root = destructible.GeneratedChunksRoot;
            string assetFolder = destructible.GeneratedAssetFolder;

            if (root != null)
            {
                Undo.DestroyObjectImmediate(root.gameObject);
            }

            destructible.EditorClearGeneratedFracture();
            EditorUtility.SetDirty(destructible);
            if (destructible.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(destructible.gameObject.scene);
            }

            if (deleteGeneratedAssets &&
                !string.IsNullOrWhiteSpace(assetFolder) &&
                assetFolder.StartsWith(GeneratedAssetsRoot, StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(assetFolder);
                AssetDatabase.Refresh();
            }
        }

        private static bool TryResolveSource(
            GameObject target,
            out MeshFilter meshFilter,
            out MeshRenderer meshRenderer,
            out string error)
        {
            meshFilter = null;
            meshRenderer = null;
            error = string.Empty;

            if (target == null)
            {
                error = "Select a scene object to fracture.";
                return false;
            }

            if (EditorUtility.IsPersistent(target))
            {
                error =
                    "Place the prefab in a scene before generating its Apex fracture hierarchy.";
                return false;
            }

            meshFilter = target.GetComponent<MeshFilter>();
            meshRenderer = target.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                error = "The selected object needs a MeshFilter with a source mesh.";
                return false;
            }

            if (meshRenderer == null)
            {
                error = "The selected object needs a MeshRenderer.";
                return false;
            }

            if (meshFilter.sharedMesh.vertexCount < 4)
            {
                error = "The source mesh does not contain enough geometry to fracture.";
                return false;
            }

            return true;
        }

        private static void ConfigureChunkRigidbody(
            Rigidbody source,
            Rigidbody chunk,
            Mesh mesh,
            float totalVolume)
        {
            float volume = Mathf.Max(0.000001f, BoundsVolume(mesh.bounds));
            float sourceMass = source != null ? Mathf.Max(0.01f, source.mass) : 1f;
            chunk.mass = Mathf.Max(0.01f, sourceMass * (volume / Mathf.Max(0.000001f, totalVolume)));
            chunk.useGravity = false;
            chunk.interpolation = source != null
                ? source.interpolation
                : RigidbodyInterpolation.Interpolate;
            chunk.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            chunk.constraints = RigidbodyConstraints.None;
            chunk.isKinematic = true;
            chunk.detectCollisions = false;
        }

        private static void CopyRendererSettings(
            MeshRenderer source,
            MeshRenderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.renderingLayerMask = source.renderingLayerMask;
        }

        private static Material[] BuildChunkMaterials(
            Material[] sourceMaterials,
            int surfaceSubmeshCount,
            Material interiorMaterial)
        {
            int surfaceCount = Mathf.Max(1, surfaceSubmeshCount);
            Material[] materials = new Material[surfaceCount + 1];
            Material fallback = sourceMaterials != null && sourceMaterials.Length > 0
                ? sourceMaterials[sourceMaterials.Length - 1]
                : null;

            for (int i = 0; i < surfaceCount; i++)
            {
                materials[i] = sourceMaterials != null && i < sourceMaterials.Length
                    ? sourceMaterials[i]
                    : fallback;
            }

            materials[surfaceCount] = interiorMaterial != null
                ? interiorMaterial
                : fallback;
            return materials;
        }

        private static Vector3 RecenterMesh(Mesh mesh)
        {
            mesh.RecalculateBounds();
            Vector3 center = mesh.bounds.center;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] -= center;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return center;
        }

        private static float BoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Abs(size.x * size.y * size.z);
        }

        private static string CreateGeneratedAssetFolder(string objectName)
        {
            EnsureFolder("Assets", "Apex Generated");
            EnsureFolder("Assets/Apex Generated", "Destruction");

            string folderName =
                SanitizeFileName(objectName) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string guid = AssetDatabase.CreateFolder(GeneratedAssetsRoot, folderName);
            return string.IsNullOrWhiteSpace(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Destructible";
            }

            string sanitized = value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                sanitized = sanitized.Replace(invalid[i], '_');
            }

            return sanitized.Replace(' ', '_');
        }

        private static void DestroyTemporaryMeshes(IEnumerable<Mesh> meshes)
        {
            if (meshes == null)
            {
                return;
            }

            foreach (Mesh mesh in meshes)
            {
                if (mesh != null && !AssetDatabase.Contains(mesh))
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}

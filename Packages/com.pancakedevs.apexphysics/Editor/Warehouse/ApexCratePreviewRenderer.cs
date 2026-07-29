using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal sealed class ApexCratePreviewRenderer : IDisposable
    {
        private const string VoidShaderName = "Hidden/Apex Physics Engine/Void Preview";

        private readonly struct PreviewMesh
        {
            public PreviewMesh(Mesh mesh, Matrix4x4 localMatrix)
            {
                Mesh = mesh;
                LocalMatrix = localMatrix;
            }

            public Mesh Mesh { get; }
            public Matrix4x4 LocalMatrix { get; }
        }

        private PreviewRenderUtility previewUtility;
        private Vector2 previewDirection = new Vector2(135f, -18f);
        private static Material sharedVoidMaterial;

        public bool HasPreview(ApexSpawnableCrate crate)
        {
            return crate != null && crate.PreviewPrefab != null &&
                   CollectMeshes(crate.PreviewPrefab, out _, out _);
        }

        public void DrawInspectorPreview(
            ApexSpawnableCrate crate,
            Rect rect,
            GUIStyle background,
            Action requestRepaint)
        {
            if (crate == null || crate.PreviewPrefab == null || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            HandlePreviewInput(rect, requestRepaint);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!CollectMeshes(crate.PreviewPrefab, out List<PreviewMesh> meshes, out Bounds bounds))
            {
                EditorGUI.HelpBox(rect, "The crate preview prefab has no MeshFilter or SkinnedMeshRenderer meshes.", MessageType.Info);
                return;
            }

            EnsurePreviewUtility();
            Material material = ResolveMaterial(crate);
            if (material == null)
            {
                EditorGUI.HelpBox(rect, "Apex could not find the Void Preview shader.", MessageType.Warning);
                return;
            }

            float maximumDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float fitScale = maximumDimension > 0.0001f ? 1.55f / maximumDimension : 1f;
            Quaternion orbit = Quaternion.Euler(-previewDirection.y, -previewDirection.x, 0f);
            Matrix4x4 modelMatrix =
                Matrix4x4.Rotate(orbit) *
                Matrix4x4.Scale(Vector3.one * fitScale * crate.PreviewScale) *
                Matrix4x4.TRS(crate.PreviewLocalOffset, crate.PreviewLocalRotation, Vector3.one) *
                Matrix4x4.Translate(-bounds.center);

            previewUtility.BeginPreview(rect, background);
            previewUtility.camera.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -3.2f),
                Quaternion.identity);
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 100f;
            previewUtility.camera.fieldOfView = 30f;
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);
            previewUtility.ambientColor = new Color(0.32f, 0.36f, 0.48f, 1f);
            previewUtility.lights[0].intensity = 1.1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 0.65f;

            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                PreviewMesh entry = meshes[meshIndex];
                if (entry.Mesh == null)
                {
                    continue;
                }

                Matrix4x4 matrix = modelMatrix * entry.LocalMatrix;
                int subMeshCount = Mathf.Max(1, entry.Mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    previewUtility.DrawMesh(entry.Mesh, matrix, material, subMesh);
                }
            }

            previewUtility.Render(true);
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
        }

        public static void DrawScenePreview(ApexCrateSpawner spawner, bool selected)
        {
            if (spawner == null || Application.isPlaying || !spawner.ShowVoidPreview ||
                (spawner.PreviewOnlyWhenSelected && !selected) || Event.current.type != EventType.Repaint)
            {
                return;
            }

            ApexSpawnableCrate crate = spawner.ResolveCrate();
            if (crate == null || crate.PreviewPrefab == null ||
                !CollectMeshes(crate.PreviewPrefab, out List<PreviewMesh> meshes, out _))
            {
                return;
            }

            Material material = ResolveMaterial(crate);
            if (material == null || !material.SetPass(0))
            {
                return;
            }

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            Matrix4x4 crateAdjustment = Matrix4x4.TRS(
                crate.PreviewLocalOffset,
                crate.PreviewLocalRotation,
                Vector3.one * crate.PreviewScale);

            for (int spawnIndex = 0; spawnIndex < spawner.SpawnCount; spawnIndex++)
            {
                Vector3 position = spawner.transform.TransformPoint(spawner.PositionStep * spawnIndex);
                Matrix4x4 spawnMatrix = Matrix4x4.TRS(position, spawner.transform.rotation, Vector3.one);

                for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
                {
                    PreviewMesh entry = meshes[meshIndex];
                    if (entry.Mesh == null)
                    {
                        continue;
                    }

                    Matrix4x4 matrix = spawnMatrix * crateAdjustment * entry.LocalMatrix;
                    int subMeshCount = Mathf.Max(1, entry.Mesh.subMeshCount);
                    for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    {
                        Graphics.DrawMeshNow(entry.Mesh, matrix, subMesh);
                    }
                }
            }

            Handles.zTest = previousZTest;
        }

        public static Material CreateEditableVoidMaterial()
        {
            Shader shader = Shader.Find(VoidShaderName);
            if (shader == null)
            {
                return null;
            }

            return new Material(shader)
            {
                name = "Apex Void Preview"
            };
        }

        public void Dispose()
        {
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
        }

        private void HandlePreviewInput(Rect rect, Action requestRepaint)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition) || current.type != EventType.MouseDrag || current.button != 0)
            {
                return;
            }

            previewDirection -= current.delta * 0.5f;
            current.Use();
            requestRepaint?.Invoke();
        }

        private static Material ResolveMaterial(ApexSpawnableCrate crate)
        {
            if (crate != null && crate.PreviewMaterial != null)
            {
                return crate.PreviewMaterial;
            }

            if (sharedVoidMaterial != null)
            {
                return sharedVoidMaterial;
            }

            Shader shader = Shader.Find(VoidShaderName);
            if (shader == null)
            {
                return null;
            }

            sharedVoidMaterial = new Material(shader)
            {
                name = "Apex Void Preview (Editor)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedVoidMaterial;
        }

        private static bool CollectMeshes(
            GameObject prefab,
            out List<PreviewMesh> meshes,
            out Bounds bounds)
        {
            meshes = new List<PreviewMesh>();
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (prefab == null)
            {
                return false;
            }

            Transform root = prefab.transform;
            Matrix4x4 rootScale = Matrix4x4.Scale(root.localScale);
            Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;
            bool hasBounds = false;

            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter filter = meshFilters[i];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 localMatrix = rootScale * rootWorldToLocal * filter.transform.localToWorldMatrix;
                meshes.Add(new PreviewMesh(mesh, localMatrix));
                EncapsulateMeshBounds(mesh.bounds, localMatrix, ref bounds, ref hasBounds);
            }

            SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                Mesh mesh = renderer != null ? renderer.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 localMatrix = rootScale * rootWorldToLocal * renderer.transform.localToWorldMatrix;
                meshes.Add(new PreviewMesh(mesh, localMatrix));
                EncapsulateMeshBounds(mesh.bounds, localMatrix, ref bounds, ref hasBounds);
            }

            return meshes.Count > 0 && hasBounds;
        }

        private static void EncapsulateMeshBounds(
            Bounds meshBounds,
            Matrix4x4 matrix,
            ref Bounds combinedBounds,
            ref bool hasBounds)
        {
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 transformed = matrix.MultiplyPoint3x4(corner);
                        if (!hasBounds)
                        {
                            combinedBounds = new Bounds(transformed, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(transformed);
                        }
                    }
                }
            }
        }
    }
}

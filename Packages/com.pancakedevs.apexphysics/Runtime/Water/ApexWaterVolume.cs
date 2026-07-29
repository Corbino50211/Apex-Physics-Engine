using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Defines an upright water volume, animated surface height, currents, trigger
    /// notifications, and an optional generated visual surface.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Apex Physics Engine/Water/Water Volume")]
    public sealed class ApexWaterVolume : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField] private bool autoFitBounds = true;
        [SerializeField, Min(0f)] private float extraDepth;

        [Header("Waves")]
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.3f);
        [SerializeField, Range(0f, 5f)] private float waveHeight = 0.25f;
        [SerializeField, Range(0f, 5f)] private float waveSpeed = 1f;
        [SerializeField, Range(0.1f, 50f)] private float waveScale = 6f;

        [Header("Rendering")]
        [SerializeField] private Color shallowColor = new Color(0.25f, 0.65f, 0.6f, 0.65f);
        [SerializeField] private Color deepColor = new Color(0.02f, 0.15f, 0.25f, 0.95f);
        [SerializeField, Min(0.001f)] private float depthMaxDistance = 6f;
        [SerializeField, Range(0f, 1f)] private float transparency = 0.85f;
        [SerializeField, Range(0f, 8f)] private float fresnelPower = 3f;
        [SerializeField] private Color fresnelColor = Color.white;
        [SerializeField] private Color foamColor = Color.white;
        [SerializeField, Min(0.001f)] private float foamDistance = 0.4f;
        [SerializeField] private bool enableCaustics;
        [SerializeField] private Texture2D causticsTexture;
        [SerializeField, Range(0f, 2f)] private float causticsStrength = 0.5f;
        [SerializeField] private bool enableDistortion = true;
        [SerializeField, Range(0f, 0.2f)] private float distortionStrength = 0.03f;

        [Header("Surface Mesh")]
        [SerializeField, Range(2, 200)] private int meshResolution = 24;
        [SerializeField] private Material waterMaterial;

        [Header("Current")]
        [SerializeField] private Vector2 currentDirection;
        [SerializeField, Min(0f)] private float currentSpeed;

        [Header("Detection")]
        [SerializeField] private LayerMask detectionMask = ~0;

        [SerializeField, HideInInspector] private Vector3 authoredLocalCenter;
        [SerializeField, HideInInspector] private Vector3 authoredLocalSize = Vector3.one;
        [SerializeField, HideInInspector] private bool hasAuthoredBounds;
        [SerializeField, HideInInspector] private string generatedSurfaceMeshPath = string.Empty;
        [SerializeField, HideInInspector] private string generatedMaterialPath = string.Empty;

        private readonly ApexWaterMath.WaveLayer[] waveLayers =
            new ApexWaterMath.WaveLayer[ApexWaterMath.MaxWaves];
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private readonly Dictionary<IApexWaterReactive, int> reactiveColliderCounts =
            new Dictionary<IApexWaterReactive, int>();
        private readonly MaterialPropertyBlock materialProperties = new MaterialPropertyBlock();

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private BoxCollider triggerCollider;
        private Matrix4x4 lastLocalToWorld;

        public event Action<Collider> ObjectEntered;
        public event Action<Collider> ObjectExited;

        public Bounds WorldBounds { get; private set; }
        public float WaveHeight => waveHeight;
        public float WaveSpeed => waveSpeed;
        public float WaveScale => waveScale;
        public Vector2 WindDirection => windDirection;
        public int MeshResolution => meshResolution;
        public Material WaterMaterial => waterMaterial;
        public Vector3 AuthoredLocalCenter => authoredLocalCenter;
        public Vector3 AuthoredLocalSize => authoredLocalSize;
        public string GeneratedSurfaceMeshPath => generatedSurfaceMeshPath;
        public string GeneratedMaterialPath => generatedMaterialPath;

        private void Reset()
        {
            CacheComponents();
            CaptureBoundsFromCurrentMesh();
            RefreshBounds();
        }

        private void OnEnable()
        {
            CacheComponents();
            CaptureBoundsIfNeeded();
            RebuildWaveLayers();
            RefreshBounds();
            ApplyMaterialProperties();
            ApexWaterManager.Register(this);
        }

        private void OnDisable()
        {
            ApexWaterManager.Unregister(this);
            NotifyAllReactiveExits();
            occupants.Clear();
        }

        private void Update()
        {
            if (autoFitBounds && transform.localToWorldMatrix != lastLocalToWorld)
            {
                RefreshBounds();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RebuildWaveLayers();
                ApplyMaterialProperties();
            }
#endif
        }

        private void OnValidate()
        {
            extraDepth = Mathf.Max(0f, extraDepth);
            waveScale = Mathf.Max(0.1f, waveScale);
            depthMaxDistance = Mathf.Max(0.001f, depthMaxDistance);
            foamDistance = Mathf.Max(0.001f, foamDistance);
            meshResolution = Mathf.Clamp(meshResolution, 2, 200);
            currentSpeed = Mathf.Max(0f, currentSpeed);
            RebuildWaveLayers();

            if (isActiveAndEnabled)
            {
                CacheComponents();
                CaptureBoundsIfNeeded();
                RefreshBounds();
                ApplyMaterialProperties();
            }
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
            }
            triggerCollider.isTrigger = true;
        }

        private void CaptureBoundsIfNeeded()
        {
            if (!hasAuthoredBounds)
            {
                CaptureBoundsFromCurrentMesh();
            }
        }

        public void CaptureBoundsFromCurrentMesh()
        {
            CacheComponents();
            Bounds bounds = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.bounds
                : new Bounds(Vector3.zero, Vector3.one);
            authoredLocalCenter = bounds.center;
            authoredLocalSize = bounds.size;
            hasAuthoredBounds = true;
        }

        public void SetAuthoredLocalBounds(Vector3 center, Vector3 size)
        {
            authoredLocalCenter = center;
            authoredLocalSize = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(size.x)),
                Mathf.Max(0.01f, Mathf.Abs(size.y)),
                Mathf.Max(0.01f, Mathf.Abs(size.z)));
            hasAuthoredBounds = true;
            RefreshBounds();
        }

        public void RefreshBounds()
        {
            CacheComponents();
            CaptureBoundsIfNeeded();

            Vector3 localMin = authoredLocalCenter - authoredLocalSize * 0.5f;
            Vector3 localMax = authoredLocalCenter + authoredLocalSize * 0.5f;
            localMin.y -= extraDepth;

            triggerCollider.center = (localMin + localMax) * 0.5f;
            triggerCollider.size = localMax - localMin;

            Vector3 firstCorner = transform.TransformPoint(localMin);
            Bounds world = new Bounds(firstCorner, Vector3.zero);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? localMin.x : localMax.x,
                    y == 0 ? localMin.y : localMax.y,
                    z == 0 ? localMin.z : localMax.z);
                world.Encapsulate(transform.TransformPoint(corner));
            }

            WorldBounds = world;
            lastLocalToWorld = transform.localToWorldMatrix;
        }

        public bool ContainsHorizontal(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 half = authoredLocalSize * 0.5f;
            return local.x >= authoredLocalCenter.x - half.x &&
                   local.x <= authoredLocalCenter.x + half.x &&
                   local.z >= authoredLocalCenter.z - half.z &&
                   local.z <= authoredLocalCenter.z + half.z;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 half = authoredLocalSize * 0.5f;
            float minimumY = authoredLocalCenter.y - half.y - extraDepth;
            float maximumY = authoredLocalCenter.y + half.y;
            return local.x >= authoredLocalCenter.x - half.x &&
                   local.x <= authoredLocalCenter.x + half.x &&
                   local.y >= minimumY && local.y <= maximumY &&
                   local.z >= authoredLocalCenter.z - half.z &&
                   local.z <= authoredLocalCenter.z + half.z;
        }

        public float GetWaterHeight(Vector3 worldPosition)
        {
            float localSurfaceY = authoredLocalCenter.y + authoredLocalSize.y * 0.5f;
            float baseHeight = transform.TransformPoint(
                new Vector3(authoredLocalCenter.x, localSurfaceY, authoredLocalCenter.z)).y;
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            return baseHeight + ApexWaterMath.SampleHeight(worldPosition, time, waveLayers);
        }

        public float GetFloorHeight(Vector3 worldPosition)
        {
            float localFloorY = authoredLocalCenter.y - authoredLocalSize.y * 0.5f - extraDepth;
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            local.y = localFloorY;
            return transform.TransformPoint(local).y;
        }

        public bool IsUnderwater(Vector3 worldPosition)
        {
            return ContainsHorizontal(worldPosition) &&
                   worldPosition.y >= GetFloorHeight(worldPosition) &&
                   worldPosition.y < GetWaterHeight(worldPosition);
        }

        public Vector3 GetCurrent(Vector3 worldPosition)
        {
            if (!ContainsHorizontal(worldPosition) || currentSpeed <= 0f ||
                currentDirection.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector2 direction = currentDirection.normalized;
            return new Vector3(direction.x, 0f, direction.y) * currentSpeed;
        }

        public Mesh CreateSurfaceMesh()
        {
            CaptureBoundsIfNeeded();
            int resolution = Mathf.Max(2, meshResolution);
            int vertexCount = (resolution + 1) * (resolution + 1);
            Mesh mesh = new Mesh { name = name + "_ApexWaterSurface" };
            mesh.indexFormat = vertexCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            Vector3 half = authoredLocalSize * 0.5f;
            float minimumX = authoredLocalCenter.x - half.x;
            float maximumX = authoredLocalCenter.x + half.x;
            float minimumZ = authoredLocalCenter.z - half.z;
            float maximumZ = authoredLocalCenter.z + half.z;
            float surfaceY = authoredLocalCenter.y + half.y;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[resolution * resolution * 6];

            for (int z = 0; z <= resolution; z++)
            for (int x = 0; x <= resolution; x++)
            {
                int index = z * (resolution + 1) + x;
                float tx = x / (float)resolution;
                float tz = z / (float)resolution;
                vertices[index] = new Vector3(
                    Mathf.Lerp(minimumX, maximumX, tx),
                    surfaceY,
                    Mathf.Lerp(minimumZ, maximumZ, tz));
                uv[index] = new Vector2(tx, tz);
            }

            int triangleIndex = 0;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int i0 = z * (resolution + 1) + x;
                int i1 = i0 + 1;
                int i2 = i0 + resolution + 1;
                int i3 = i2 + 1;
                triangles[triangleIndex++] = i0;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i3;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void AssignSurfaceMesh(Mesh mesh)
        {
            CacheComponents();
            meshFilter.sharedMesh = mesh;
            ApplyMaterialProperties();
        }

        public void AssignWaterMaterial(Material material)
        {
            waterMaterial = material;
            CacheComponents();
            meshRenderer.sharedMaterial = material;
            ApplyMaterialProperties();
        }

        public void ApplyMaterialProperties()
        {
            CacheComponents();
            if (meshRenderer == null)
            {
                return;
            }

            if (waterMaterial != null && meshRenderer.sharedMaterial != waterMaterial)
            {
                meshRenderer.sharedMaterial = waterMaterial;
            }

            meshRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetColor("_ShallowColor", shallowColor);
            materialProperties.SetColor("_DeepColor", deepColor);
            materialProperties.SetFloat("_DepthMaxDistance", depthMaxDistance);
            materialProperties.SetFloat("_Transparency", transparency);
            materialProperties.SetFloat("_FresnelPower", fresnelPower);
            materialProperties.SetColor("_FresnelColor", fresnelColor);
            materialProperties.SetColor("_FoamColor", foamColor);
            materialProperties.SetFloat("_FoamDistance", foamDistance);
            materialProperties.SetFloat("_WaveHeight", waveHeight);
            materialProperties.SetFloat("_WaveSpeed", waveSpeed);
            materialProperties.SetFloat("_WaveScale", waveScale);
            materialProperties.SetFloat("_WindDirX", windDirection.x);
            materialProperties.SetFloat("_WindDirZ", windDirection.y);
            materialProperties.SetFloat("_EnableCaustics", enableCaustics ? 1f : 0f);
            materialProperties.SetTexture("_CausticsTex", causticsTexture);
            materialProperties.SetFloat("_CausticsStrength", causticsStrength);
            materialProperties.SetFloat("_EnableDistortion", enableDistortion ? 1f : 0f);
            materialProperties.SetFloat("_DistortionStrength", distortionStrength);
            meshRenderer.SetPropertyBlock(materialProperties);
        }

        private void RebuildWaveLayers()
        {
            ApexWaterMath.BuildLayers(
                windDirection,
                waveHeight,
                waveSpeed,
                waveScale,
                waveLayers);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !LayerIsDetected(other.gameObject.layer) ||
                !occupants.Add(other))
            {
                return;
            }

            ObjectEntered?.Invoke(other);
            IApexWaterReactive reactive = FindReactive(other);
            if (reactive == null)
            {
                return;
            }

            reactiveColliderCounts.TryGetValue(reactive, out int count);
            reactiveColliderCounts[reactive] = count + 1;
            if (count == 0)
            {
                reactive.OnApexWaterEnter(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || !occupants.Remove(other))
            {
                return;
            }

            ObjectExited?.Invoke(other);
            IApexWaterReactive reactive = FindReactive(other);
            if (reactive == null || !reactiveColliderCounts.TryGetValue(reactive, out int count))
            {
                return;
            }

            count--;
            if (count <= 0)
            {
                reactiveColliderCounts.Remove(reactive);
                reactive.OnApexWaterExit(this);
            }
            else
            {
                reactiveColliderCounts[reactive] = count;
            }
        }

        private bool LayerIsDetected(int layer)
        {
            return (detectionMask.value & (1 << layer)) != 0;
        }

        private static IApexWaterReactive FindReactive(Collider collider)
        {
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IApexWaterReactive reactive)
                {
                    return reactive;
                }
            }

            return null;
        }

        private void NotifyAllReactiveExits()
        {
            if (reactiveColliderCounts.Count == 0)
            {
                return;
            }

            IApexWaterReactive[] reactives = new IApexWaterReactive[reactiveColliderCounts.Count];
            reactiveColliderCounts.Keys.CopyTo(reactives, 0);
            reactiveColliderCounts.Clear();
            for (int i = 0; i < reactives.Length; i++)
            {
                reactives[i]?.OnApexWaterExit(this);
            }
        }

#if UNITY_EDITOR
        public void EditorSetGeneratedAssetPaths(string meshPath, string materialPath)
        {
            generatedSurfaceMeshPath = meshPath ?? string.Empty;
            generatedMaterialPath = materialPath ?? string.Empty;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (!hasAuthoredBounds)
            {
                CacheComponents();
                CaptureBoundsFromCurrentMesh();
            }
            RefreshBounds();
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.45f);
            Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.06f);
            Gizmos.DrawCube(WorldBounds.center, WorldBounds.size);
        }
    }
}

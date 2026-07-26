using System;
using Unity.AI.Navigation;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Controls an AI Navigation NavMeshSurface and provides consistent manual,
    /// pre-play, startup, and interval-based rebuild behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class ApexNavMeshAutoBaker : MonoBehaviour
    {
        [Header("Bake Timing")]
        [SerializeField] private ApexNavMeshBakeMode bakeMode = ApexNavMeshBakeMode.BeforePlayMode;
        [SerializeField, Min(0.25f)] private float runtimeRebuildInterval = 5f;
        [SerializeField] private bool onlyBuildWhenDirty = true;

        [Header("Build Behavior")]
        [SerializeField] private bool removeExistingDataBeforeBuild = true;
        [SerializeField] private bool logBuilds;

        private NavMeshSurface cachedSurface;
        private bool isDirty = true;
        private float nextRuntimeBuildTime;
        private int buildCount;
        private double lastBuildTime = -1d;

        public event Action<ApexNavMeshAutoBaker> Built;
        public event Action<ApexNavMeshAutoBaker> Cleared;

        public NavMeshSurface Surface
        {
            get
            {
                CacheSurface();
                return cachedSurface;
            }
        }

        public ApexNavMeshBakeMode BakeMode => bakeMode;
        public bool OnlyBuildWhenDirty => onlyBuildWhenDirty;
        public bool IsDirty => isDirty;
        public int BuildCount => buildCount;
        public double LastBuildTime => lastBuildTime;
        public bool ShouldBuildBeforePlayMode => bakeMode == ApexNavMeshBakeMode.BeforePlayMode;

        private void Reset()
        {
            CacheSurface();
            MarkDirty();
        }

        private void Awake()
        {
            CacheSurface();
        }

        private void OnEnable()
        {
            nextRuntimeBuildTime = Time.unscaledTime + runtimeRebuildInterval;
        }

        private void Start()
        {
            if (bakeMode == ApexNavMeshBakeMode.OnStart)
            {
                BuildIfNeeded();
            }
        }

        private void Update()
        {
            if (bakeMode != ApexNavMeshBakeMode.RuntimeInterval ||
                Time.unscaledTime < nextRuntimeBuildTime)
            {
                return;
            }

            nextRuntimeBuildTime = Time.unscaledTime + runtimeRebuildInterval;
            BuildIfNeeded();
        }

        /// <summary>Marks the surface as needing a rebuild.</summary>
        public void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>Builds only when allowed by the dirty-state setting.</summary>
        public bool BuildIfNeeded()
        {
            if (onlyBuildWhenDirty && !isDirty)
            {
                return false;
            }

            return BuildNow();
        }

        /// <summary>Immediately rebuilds the NavMeshSurface.</summary>
        public bool BuildNow()
        {
            CacheSurface();
            if (cachedSurface == null || !isActiveAndEnabled)
            {
                return false;
            }

            if (removeExistingDataBeforeBuild)
            {
                cachedSurface.RemoveData();
            }

            cachedSurface.BuildNavMesh();
            isDirty = false;
            buildCount++;
            lastBuildTime = Time.realtimeSinceStartupAsDouble;

            if (logBuilds)
            {
                Debug.Log($"[Apex Navigation] Built NavMesh '{name}' (build {buildCount}).", this);
            }

            Built?.Invoke(this);
            return true;
        }

        /// <summary>Removes the surface's currently loaded NavMesh data.</summary>
        public void Clear()
        {
            CacheSurface();
            if (cachedSurface == null)
            {
                return;
            }

            cachedSurface.RemoveData();
            isDirty = true;
            Cleared?.Invoke(this);
        }

        private void CacheSurface()
        {
            if (cachedSurface == null)
            {
                cachedSurface = GetComponent<NavMeshSurface>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            runtimeRebuildInterval = Mathf.Max(0.25f, runtimeRebuildInterval);
            CacheSurface();
            MarkDirty();
        }
#endif
    }
}

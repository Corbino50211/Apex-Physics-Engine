using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Lightweight scene marker that resolves an Apex crate and creates its prefab
    /// after the level has loaded. EndOfFrame is the recommended default because it
    /// gives warehouse and scene bootstrap objects time to register their pallets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexCrateSpawner : MonoBehaviour
    {
        [Header("Crate")]
        [SerializeField] private ApexSpawnableCrate crate;
        [SerializeField] private string barcode;

        [Header("Spawn")]
        [SerializeField] private ApexCrateSpawnTiming spawnTiming = ApexCrateSpawnTiming.EndOfFrame;
        [SerializeField, Min(1)] private int spawnCount = 1;
        [SerializeField] private Vector3 positionStep;
        [SerializeField] private bool spawnAsChildren;
        [SerializeField] private Transform parentOverride;

        [Header("Lifetime")]
        [SerializeField] private bool spawnOnce = true;
        [SerializeField] private bool clearPreviousBeforeSpawn = true;
        [SerializeField] private bool warnWhenMissing = true;

        private readonly List<GameObject> spawnedInstances = new List<GameObject>();
        private bool hasSpawned;

        public ApexSpawnableCrate Crate => crate;
        public string Barcode => crate != null ? crate.Barcode : barcode;
        public ApexCrateSpawnTiming SpawnTiming => spawnTiming;
        public IReadOnlyList<GameObject> SpawnedInstances => spawnedInstances;
        public bool HasSpawned => hasSpawned;

        private void Awake()
        {
            if (spawnTiming == ApexCrateSpawnTiming.Awake)
            {
                SpawnAll();
            }
        }

        private IEnumerator Start()
        {
            if (spawnTiming == ApexCrateSpawnTiming.Start)
            {
                SpawnAll();
                yield break;
            }

            if (spawnTiming == ApexCrateSpawnTiming.EndOfFrame)
            {
                yield return new WaitForEndOfFrame();
                SpawnAll();
            }
        }

        public void SetCrate(ApexSpawnableCrate newCrate)
        {
            crate = newCrate;
            barcode = newCrate != null ? newCrate.Barcode : string.Empty;
        }

        public void SetBarcode(string newBarcode)
        {
            barcode = newBarcode;
            crate = null;
        }

        public ApexSpawnableCrate ResolveCrate()
        {
            if (crate != null)
            {
                return crate;
            }

            if (string.IsNullOrWhiteSpace(barcode))
            {
                return null;
            }

            ApexWarehouse warehouse = ApexWarehouse.Instance;
            if (warehouse == null && Application.isPlaying)
            {
                warehouse = ApexWarehouse.GetOrCreate();
            }

            return warehouse != null && warehouse.TryGetCrate(barcode, out ApexSpawnableCrate resolved)
                ? resolved
                : null;
        }

        public int SpawnAll()
        {
            RemoveDestroyedInstances();

            if (spawnOnce && hasSpawned)
            {
                return 0;
            }

            ApexSpawnableCrate resolvedCrate = ResolveCrate();
            if (resolvedCrate == null)
            {
                if (warnWhenMissing)
                {
                    Debug.LogWarning(
                        string.IsNullOrWhiteSpace(barcode)
                            ? $"Apex crate spawner '{name}' has no crate or barcode assigned."
                            : $"Apex crate spawner '{name}' could not resolve barcode '{barcode}'.",
                        this);
                }

                return 0;
            }

            if (clearPreviousBeforeSpawn)
            {
                ClearSpawned();
            }

            ApexWarehouse warehouse = ApexWarehouse.GetOrCreate();
            warehouse.RegisterCrate(resolvedCrate);

            Transform spawnParent = parentOverride != null
                ? parentOverride
                : (spawnAsChildren ? transform : null);

            int spawnedCount = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 position = transform.TransformPoint(positionStep * i);
                GameObject instanceObject = warehouse.Spawn(
                    resolvedCrate,
                    position,
                    transform.rotation,
                    spawnParent,
                    this);

                if (instanceObject == null)
                {
                    continue;
                }

                spawnedInstances.Add(instanceObject);
                spawnedCount++;
            }

            if (spawnedCount > 0)
            {
                hasSpawned = true;
            }

            return spawnedCount;
        }

        public void ClearSpawned()
        {
            for (int i = spawnedInstances.Count - 1; i >= 0; i--)
            {
                GameObject instanceObject = spawnedInstances[i];
                spawnedInstances.RemoveAt(i);

                if (instanceObject == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(instanceObject);
                }
                else
                {
                    DestroyImmediate(instanceObject);
                }
            }
        }

        public void ResetSpawnState(bool clearSpawnedObjects = false)
        {
            if (clearSpawnedObjects)
            {
                ClearSpawned();
            }

            hasSpawned = false;
        }

        private void RemoveDestroyedInstances()
        {
            spawnedInstances.RemoveAll(instanceObject => instanceObject == null);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            spawnCount = Mathf.Max(1, spawnCount);
            if (crate != null)
            {
                crate.EnsureBarcode();
                barcode = crate.Barcode;
            }
        }

        private void OnDrawGizmos()
        {
            int count = Mathf.Max(1, spawnCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = transform.TransformPoint(positionStep * i);
                Gizmos.DrawWireCube(position, Vector3.one * 0.2f);
                Gizmos.DrawLine(position, position + transform.forward * 0.4f);
            }
        }
#endif
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Runtime registry for Apex pallets and crates. Barcodes resolve to crate assets,
    /// allowing scenes and future save/networking systems to spawn content without
    /// depending on prefab paths.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ApexWarehouse : MonoBehaviour
    {
        private static ApexWarehouse instance;

        [Header("Lifetime")]
        [SerializeField] private bool persistBetweenScenes = true;

        [Header("Preloaded Pallets")]
        [SerializeField] private bool registerPalletsOnAwake = true;
        [SerializeField] private List<ApexPallet> pallets = new List<ApexPallet>();

        private readonly Dictionary<string, ApexCrate> cratesByBarcode =
            new Dictionary<string, ApexCrate>(StringComparer.OrdinalIgnoreCase);

        public static ApexWarehouse Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ApexWarehouse>();
                }

                return instance;
            }
        }

        public static ApexWarehouse GetOrCreate()
        {
            ApexWarehouse warehouse = Instance;
            if (warehouse != null)
            {
                return warehouse;
            }

            GameObject warehouseObject = new GameObject("Apex Warehouse");
            return warehouseObject.AddComponent<ApexWarehouse>();
        }

        public event Action<ApexCrate> CrateRegistered;
        public event Action<ApexSpawnableCrate, GameObject> InstanceSpawned;

        public int RegisteredCrateCount => cratesByBarcode.Count;
        public IReadOnlyList<ApexPallet> Pallets => pallets;
        public IEnumerable<ApexCrate> RegisteredCrates => cratesByBarcode.Values;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (persistBetweenScenes)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }

                DontDestroyOnLoad(gameObject);
            }

            if (registerPalletsOnAwake)
            {
                RebuildRegistry();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void RebuildRegistry()
        {
            cratesByBarcode.Clear();

            for (int i = 0; i < pallets.Count; i++)
            {
                RegisterPallet(pallets[i]);
            }
        }

        public bool RegisterPallet(ApexPallet pallet)
        {
            if (pallet == null)
            {
                return false;
            }

            bool registeredAny = false;
            IReadOnlyList<ApexCrate> crates = pallet.Crates;
            for (int i = 0; i < crates.Count; i++)
            {
                registeredAny |= RegisterCrate(crates[i]);
            }

            return registeredAny;
        }

        public bool RegisterCrate(ApexCrate crate)
        {
            if (crate == null)
            {
                return false;
            }

            crate.EnsureBarcode();
            string barcode = crate.Barcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                Debug.LogError($"Apex crate '{crate.name}' does not have a barcode.", crate);
                return false;
            }

            if (cratesByBarcode.TryGetValue(barcode, out ApexCrate existing))
            {
                if (existing == crate)
                {
                    return false;
                }

                Debug.LogError(
                    $"Duplicate Apex crate barcode '{barcode}' on '{existing.name}' and '{crate.name}'. " +
                    "Regenerate one barcode before using these crates together.",
                    crate);
                return false;
            }

            cratesByBarcode.Add(barcode, crate);
            CrateRegistered?.Invoke(crate);
            return true;
        }

        public bool TryGetCrate(string barcode, out ApexCrate crate)
        {
            crate = null;
            return !string.IsNullOrWhiteSpace(barcode) &&
                   cratesByBarcode.TryGetValue(barcode, out crate);
        }

        public bool TryGetCrate<TCrate>(string barcode, out TCrate crate)
            where TCrate : ApexCrate
        {
            crate = null;
            if (!TryGetCrate(barcode, out ApexCrate result))
            {
                return false;
            }

            crate = result as TCrate;
            return crate != null;
        }

        public GameObject Spawn(
            ApexSpawnableCrate crate,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            ApexCrateSpawner sourceSpawner = null)
        {
            if (crate == null)
            {
                return null;
            }

            RegisterCrate(crate);
            GameObject spawnedObject = crate.Instantiate(position, rotation, parent);
            if (spawnedObject == null)
            {
                return null;
            }

            ApexCrateInstance crateInstance = spawnedObject.GetComponent<ApexCrateInstance>();
            if (crateInstance == null)
            {
                crateInstance = spawnedObject.AddComponent<ApexCrateInstance>();
            }

            crateInstance.Initialize(crate, sourceSpawner);
            InstanceSpawned?.Invoke(crate, spawnedObject);
            return spawnedObject;
        }

        public bool TrySpawn(
            string barcode,
            Vector3 position,
            Quaternion rotation,
            out GameObject instanceObject,
            Transform parent = null,
            ApexCrateSpawner sourceSpawner = null)
        {
            instanceObject = null;
            if (!TryGetCrate(barcode, out ApexSpawnableCrate crate))
            {
                return false;
            }

            instanceObject = Spawn(crate, position, rotation, parent, sourceSpawner);
            return instanceObject != null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            pallets ??= new List<ApexPallet>();
            pallets.RemoveAll(pallet => pallet == null);
        }
#endif
    }
}

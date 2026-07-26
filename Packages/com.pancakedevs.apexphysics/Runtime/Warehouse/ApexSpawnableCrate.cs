using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>A crate that produces a prefab instance through the Apex Warehouse.</summary>
    [CreateAssetMenu(
        fileName = "Apex Spawnable Crate",
        menuName = "Apex Physics Engine/Warehouse/Spawnable Crate")]
    public sealed class ApexSpawnableCrate : ApexCrate
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool activateOnSpawn = true;

        public override ApexCrateKind Kind => ApexCrateKind.Spawnable;
        public GameObject Prefab => prefab;
        public bool ActivateOnSpawn => activateOnSpawn;

        public void SetPrefab(GameObject value)
        {
            prefab = value;
        }

        public void SetActivateOnSpawn(bool value)
        {
            activateOnSpawn = value;
        }

        public GameObject Instantiate(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError($"Apex crate '{Title}' has no prefab assigned.", this);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.SetActive(activateOnSpawn);
            return instance;
        }
    }
}

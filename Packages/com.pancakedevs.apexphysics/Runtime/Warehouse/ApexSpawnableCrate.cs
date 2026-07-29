using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>A crate that produces a prefab instance through the Apex Warehouse.</summary>
    [CreateAssetMenu(
        fileName = "Apex Spawnable Crate",
        menuName = "Apex Physics Engine/Warehouse/Spawnable Crate")]
    public sealed class ApexSpawnableCrate : ApexCrate
    {
        [Header("Spawned Content")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool activateOnSpawn = true;

        [Header("Editor Void Preview")]
        [Tooltip("Optional simplified preview model. When empty, Apex previews the spawned prefab.")]
        [SerializeField] private GameObject previewPrefab;
        [Tooltip("Optional material override. When empty, Apex uses its generated Void Preview material.")]
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Vector3 previewLocalOffset;
        [SerializeField] private Vector3 previewLocalEuler;
        [SerializeField, Min(0.001f)] private float previewScale = 1f;

        public override ApexCrateKind Kind => ApexCrateKind.Spawnable;
        public GameObject Prefab => prefab;
        public bool ActivateOnSpawn => activateOnSpawn;
        public GameObject PreviewPrefab => previewPrefab != null ? previewPrefab : prefab;
        public Material PreviewMaterial => previewMaterial;
        public Vector3 PreviewLocalOffset => previewLocalOffset;
        public Quaternion PreviewLocalRotation => Quaternion.Euler(previewLocalEuler);
        public Vector3 PreviewLocalEuler => previewLocalEuler;
        public float PreviewScale => Mathf.Max(0.001f, previewScale);

        public void SetPrefab(GameObject value)
        {
            prefab = value;
        }

        public void SetActivateOnSpawn(bool value)
        {
            activateOnSpawn = value;
        }

        public void SetPreviewPrefab(GameObject value)
        {
            previewPrefab = value;
        }

        public void SetPreviewMaterial(Material value)
        {
            previewMaterial = value;
        }

        public void SetPreviewTransform(Vector3 localOffset, Vector3 localEuler, float scale)
        {
            previewLocalOffset = localOffset;
            previewLocalEuler = localEuler;
            previewScale = Mathf.Max(0.001f, scale);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            previewScale = Mathf.Max(0.001f, previewScale);
        }
#endif
    }
}

using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Identifies a runtime object created from an Apex crate. The instance ID is
    /// useful for save systems, networking adapters, reset tools, and diagnostics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexCrateInstance : MonoBehaviour
    {
        [SerializeField] private ApexSpawnableCrate sourceCrate;
        [SerializeField, HideInInspector] private string sourceBarcode;
        [SerializeField, HideInInspector] private string instanceId;
        [SerializeField] private ApexCrateSpawner sourceSpawner;

        public ApexSpawnableCrate SourceCrate => sourceCrate;
        public string SourceBarcode => sourceBarcode;
        public string InstanceId => instanceId;
        public ApexCrateSpawner SourceSpawner => sourceSpawner;

        public void Initialize(ApexSpawnableCrate crate, ApexCrateSpawner spawner)
        {
            sourceCrate = crate;
            sourceBarcode = crate != null ? crate.Barcode : string.Empty;
            sourceSpawner = spawner;
            instanceId = Guid.NewGuid().ToString("N");
        }

        public void RegenerateInstanceId()
        {
            instanceId = Guid.NewGuid().ToString("N");
        }

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                RegenerateInstanceId();
            }

            if (sourceCrate != null && string.IsNullOrWhiteSpace(sourceBarcode))
            {
                sourceBarcode = sourceCrate.Barcode;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>Groups crates that should be registered together in the Apex Warehouse.</summary>
    [CreateAssetMenu(
        fileName = "Apex Pallet",
        menuName = "Apex Physics Engine/Warehouse/Pallet")]
    public sealed class ApexPallet : ScriptableObject
    {
        [SerializeField, HideInInspector] private string barcode;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private List<ApexCrate> crates = new List<ApexCrate>();

        public string Barcode => barcode;
        public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
        public string Description => description;
        public IReadOnlyList<ApexCrate> Crates => crates;

        public void EnsureBarcode()
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = Guid.NewGuid().ToString("N");
            }
        }

        public void RegenerateBarcode()
        {
            barcode = Guid.NewGuid().ToString("N");
        }

        public void SetTitle(string value)
        {
            title = value;
        }

        public void SetDescription(string value)
        {
            description = value;
        }

        public void SetCrates(IEnumerable<ApexCrate> values)
        {
            crates.Clear();
            if (values == null)
            {
                return;
            }

            foreach (ApexCrate crate in values)
            {
                if (crate != null && !crates.Contains(crate))
                {
                    crates.Add(crate);
                }
            }
        }

        public bool AddCrate(ApexCrate crate)
        {
            if (crate == null || crates.Contains(crate))
            {
                return false;
            }

            crates.Add(crate);
            return true;
        }

        private void OnEnable()
        {
            EnsureBarcode();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureBarcode();
            crates ??= new List<ApexCrate>();
            crates.RemoveAll(crate => crate == null);
        }
#endif
    }
}

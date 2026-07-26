using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Base asset stored in the Apex Warehouse. Every crate has a stable barcode
    /// so scenes, save files, networking adapters, and tools can reference content
    /// without depending on a prefab path.
    /// </summary>
    public abstract class ApexCrate : ScriptableObject
    {
        [SerializeField, HideInInspector] private string barcode;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Barcode => barcode;
        public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
        public string Description => description;
        public IReadOnlyList<string> Tags => tags;
        public abstract ApexCrateKind Kind { get; }

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

        public void SetTags(string[] values)
        {
            tags = values ?? Array.Empty<string>();
        }

        protected virtual void OnEnable()
        {
            EnsureBarcode();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            EnsureBarcode();
            tags ??= Array.Empty<string>();
        }
#endif
    }
}

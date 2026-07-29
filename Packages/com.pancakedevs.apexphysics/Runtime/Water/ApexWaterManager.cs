using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Static registry and query surface for active Apex water volumes.
    /// </summary>
    public static class ApexWaterManager
    {
        private static readonly List<ApexWaterVolume> VolumesInternal =
            new List<ApexWaterVolume>();

        public static IReadOnlyList<ApexWaterVolume> Volumes => VolumesInternal;

        internal static void Register(ApexWaterVolume volume)
        {
            if (volume != null && !VolumesInternal.Contains(volume))
            {
                VolumesInternal.Add(volume);
            }
        }

        internal static void Unregister(ApexWaterVolume volume)
        {
            VolumesInternal.Remove(volume);
        }

        /// <summary>
        /// Finds the highest water surface whose footprint contains the point and
        /// whose floor is not above it. This behaves predictably with overlapping
        /// pools, rivers, and ocean volumes.
        /// </summary>
        public static ApexWaterVolume FindVolume(Vector3 worldPosition)
        {
            ApexWaterVolume best = null;
            float bestSurface = float.NegativeInfinity;

            for (int i = 0; i < VolumesInternal.Count; i++)
            {
                ApexWaterVolume volume = VolumesInternal[i];
                if (volume == null || !volume.isActiveAndEnabled ||
                    !volume.ContainsHorizontal(worldPosition) ||
                    worldPosition.y < volume.GetFloorHeight(worldPosition))
                {
                    continue;
                }

                float surface = volume.GetWaterHeight(worldPosition);
                if (surface > bestSurface)
                {
                    best = volume;
                    bestSurface = surface;
                }
            }

            return best;
        }

        public static bool TryGetWaterHeight(Vector3 worldPosition, out float height)
        {
            ApexWaterVolume volume = FindVolume(worldPosition);
            if (volume == null)
            {
                height = float.NegativeInfinity;
                return false;
            }

            height = volume.GetWaterHeight(worldPosition);
            return true;
        }

        public static float GetWaterHeight(Vector3 worldPosition)
        {
            return TryGetWaterHeight(worldPosition, out float height)
                ? height
                : float.NegativeInfinity;
        }

        public static bool IsUnderwater(Vector3 worldPosition)
        {
            ApexWaterVolume volume = FindVolume(worldPosition);
            return volume != null && volume.IsUnderwater(worldPosition);
        }

        public static Vector3 GetCurrent(Vector3 worldPosition)
        {
            ApexWaterVolume volume = FindVolume(worldPosition);
            return volume != null ? volume.GetCurrent(worldPosition) : Vector3.zero;
        }
    }
}

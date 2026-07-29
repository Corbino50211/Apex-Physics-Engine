using UnityEditor;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexLightingMenu
    {
        private const string Root = "Apex Physics Engine/Lighting/";

        [MenuItem(Root + "Open Lighting Baker", false, 50)]
        private static void OpenLightingBaker()
        {
            ApexLightingBaker.OpenWindow();
        }

        [MenuItem(Root + "Bake and Enter Play Mode", false, 51)]
        private static void BakeAndEnterPlayMode()
        {
            ApexLightingBaker.BakeAndPlay();
        }

        [MenuItem(Root + "Bake Lighting Now", false, 52)]
        private static void BakeLightingNow()
        {
            ApexLightingBaker.BakeNow(false, true);
        }

        [MenuItem(Root + "Bake Reflection Probes", false, 53)]
        private static void BakeReflectionProbes()
        {
            ApexLightingBaker.BakeReflectionProbes();
        }

        [MenuItem(Root + "Apply Selected Preset", false, 54)]
        private static void ApplySelectedPreset()
        {
            ApexLightingBaker.ApplySelectedPreset();
        }

        [MenuItem(Root + "Mark Lighting Dirty", false, 70)]
        private static void MarkLightingDirty()
        {
            ApexLightingBaker.MarkDirty();
        }

        [MenuItem(Root + "Clear Lighting Data", false, 71)]
        private static void ClearLightingData()
        {
            ApexLightingBaker.ClearLightingData();
        }

        [MenuItem(Root + "Cancel Active Bake", false, 72)]
        private static void CancelActiveBake()
        {
            ApexLightingBaker.CancelBake();
        }

        [MenuItem(Root + "Cancel Active Bake", true)]
        private static bool ValidateCancelActiveBake()
        {
            return ApexLightingBaker.IsBaking;
        }
    }
}

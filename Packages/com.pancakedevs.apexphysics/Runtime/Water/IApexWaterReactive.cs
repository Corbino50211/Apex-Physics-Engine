namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Receives transitions into and out of Apex water volumes. ApexWaterVolume
    /// reference-counts child colliders, so a multi-collider body receives one enter
    /// callback when its first collider enters and one exit callback after its last
    /// collider leaves.
    /// </summary>
    public interface IApexWaterReactive
    {
        void OnApexWaterEnter(ApexWaterVolume volume);
        void OnApexWaterExit(ApexWaterVolume volume);
    }
}

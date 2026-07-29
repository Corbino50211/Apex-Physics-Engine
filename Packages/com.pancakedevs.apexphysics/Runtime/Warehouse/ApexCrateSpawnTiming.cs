namespace PancakeDevs.ApexPhysics
{
    /// <summary>Controls when a scene crate marker creates its prefab instance.</summary>
    public enum ApexCrateSpawnTiming
    {
        Awake = 0,
        Start = 1,
        EndOfFrame = 2,
        Manual = 3
    }
}

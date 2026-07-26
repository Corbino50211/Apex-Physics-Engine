namespace PancakeDevs.ApexPhysics
{
    /// <summary>Controls when an Apex NavMesh surface rebuilds.</summary>
    public enum ApexNavMeshBakeMode
    {
        Manual = 0,
        BeforePlayMode = 1,
        OnStart = 2,
        RuntimeInterval = 3
    }
}

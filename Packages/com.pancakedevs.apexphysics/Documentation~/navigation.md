# Apex Navigation

Apex 0.0.3 adds the navigation foundation used by future physical NPCs.

## Create a navigation surface

1. Choose **GameObject > Apex Physics > Create Navigation Surface**.
2. Configure the added `NavMeshSurface` collection settings.
3. Configure `ApexNavMeshAutoBaker`.
4. Use **Build Now** in the inspector, or leave the default **Before Play Mode** bake mode enabled.

## Bake modes

- **Manual** only builds when `BuildNow` or `BuildIfNeeded` is called.
- **Before Play Mode** builds dirty surfaces while Unity exits Edit Mode.
- **On Start** builds once when the scene starts.
- **Runtime Interval** checks at a configurable interval and only rebuilds when dirty by default.

Call `MarkDirty()` after meaningful runtime geometry changes. Moving small props should normally use `NavMeshObstacle` carving instead of rebuilding the entire surface.

## Create an NPC navigator

1. Select the NPC root.
2. Choose **GameObject > Apex Physics > Make Selected Object an NPC Navigator**.
3. Assign a target to `ApexNPCNavigator`, or call `SetTarget` / `SetDestination` from code.

`ApexNPCNavigator` configures its `NavMeshAgent` with transform position and rotation updates disabled. It exposes `DesiredVelocity` and `SteeringTarget` for the upcoming force-driven NPC motor rather than allowing the NavMeshAgent to teleport or directly move the NPC body.

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class SendNPCToTarget : MonoBehaviour
{
    [SerializeField] private ApexNPCNavigator navigator;
    [SerializeField] private Transform target;

    public void Go()
    {
        navigator.SetTarget(target);
    }
}
```

## Current limitation

Version 0.0.3 plans routes but does not yet apply walking forces. The physical NPC motor, turning, balance, falling, and recovery are the next NPC milestone.

# Apex Navigation and Basic NPC Movement

Apex 0.0.3 introduced automatic NavMesh baking and path planning. Apex 0.0.4 adds the first force-driven NPC motor.

## Create a navigation surface

1. Choose **Apex Physics Engine > Navigation > Create Navigation Surface**.
2. Configure the added `NavMeshSurface` collection settings.
3. Configure `ApexNavMeshAutoBaker`.
4. Use **Build Now** in the inspector, or leave the default **Before Play Mode** bake mode enabled.

## Bake modes

- **Manual** only builds when `BuildNow` or `BuildIfNeeded` is called.
- **Before Play Mode** builds dirty surfaces while Unity exits Edit Mode.
- **On Start** builds once when the scene starts.
- **Runtime Interval** checks at a configurable interval and only rebuilds when dirty by default.

Call `MarkDirty()` after meaningful runtime geometry changes. Moving small props should normally use `NavMeshObstacle` carving instead of rebuilding the entire surface.

## Create a moving basic NPC

1. Select the NPC root object.
2. Choose **Apex Physics Engine > Navigation > Make Selected Object a Basic NPC**.
3. Assign a target to `ApexNPCNavigator`, or call `SetTarget` / `SetDestination` from code.
4. Enter Play Mode after the NavMesh has been built.

The setup command adds:

- a Collider when one is missing
- `Rigidbody`
- `ApexBody`
- `NavMeshAgent`
- `ApexNPCNavigator`
- `ApexNPCMotor`

`ApexNPCNavigator` calculates the path with direct NavMesh transform movement disabled. `ApexNPCMotor` reads the steering target and pushes the Rigidbody using forces and torque.

Existing 0.0.3 NPC objects may only have the navigator. Select one and click **Add Physical NPC Motor** in the navigator inspector, or run **Make Selected Object a Basic NPC** again.

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

## Motor behavior

`ApexNPCMotor` controls:

- movement speed
- acceleration and braking
- maximum movement force
- physical turning torque
- maximum turn speed
- upright spring and damping
- automatic Rigidbody interpolation and continuous collision detection

The basic motor keeps the NPC upright and responsive while still allowing it to be pushed. Full active-ragdoll balance, falling states, recovery animations, physical legs, and advanced obstacle interaction remain later NPC milestones.

# Apex Navigation, Movement, and NPC Behaviors

Apex 0.0.3 introduced automatic NavMesh baking and path planning. Apex 0.0.4 added force-driven NPC movement. Apex 0.0.5 adds high-level Idle, Wander, and Chase behaviors.

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

## Create a basic NPC

1. Select the NPC root object.
2. Choose **Apex Physics Engine > Navigation > Make Selected Object a Basic NPC**.
3. Build the NavMesh.
4. Choose a Starting Mode on `ApexNPCBrain`.
5. Enter Play Mode.

The setup command adds:

- a Collider when one is missing
- `Rigidbody`
- `ApexBody`
- `NavMeshAgent`
- `ApexNPCNavigator`
- `ApexNPCMotor`
- `ApexNPCBrain`

`ApexNPCNavigator` calculates paths with direct NavMesh transform movement disabled. `ApexNPCMotor` reads the steering target and pushes the Rigidbody using forces and torque. `ApexNPCBrain` selects destinations and changes behavior modes.

Running **Make Selected Object a Basic NPC** again upgrades an older NPC without removing its existing settings. The navigator inspector also provides repair buttons for missing motor or brain components.

## Behavior modes

### Idle

The navigator destination is cleared and the NPC remains in place.

### Wander

The NPC remembers its starting position as its home point, waits for a random interval, and selects valid NavMesh destinations inside the configured wander radius. Use **Set Home Here** at runtime to move the wander center.

### Chase

The NPC follows the assigned Chase Target until another mode is selected. Call `SetChaseTarget` from gameplay code to begin or stop an indefinite chase.

## Hard-impact retaliation

When **Chase When Hit** is enabled, `ApexNPCBrain` listens to collision data from `ApexBody`.

A collision triggers retaliation when either:

- its relative speed reaches **Minimum Impact Speed**, or
- its impulse reaches **Minimum Impact Impulse**.

The NPC chases the Rigidbody or Apex body that caused the hit. The default chase duration is 10 seconds. Additional hard hits refresh the timer and can replace the chase target. When the timer expires, the NPC returns to the configured **Mode After Impact Chase**, which defaults to Wander.

## Runtime example

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class ControlApexNPC : MonoBehaviour
{
    [SerializeField] private ApexNPCBrain brain;
    [SerializeField] private Transform target;

    public void Wander()
    {
        brain.SetMode(ApexNPCBehaviorMode.Wander);
    }

    public void Chase()
    {
        brain.SetChaseTarget(target);
    }

    public void Stop()
    {
        brain.SetMode(ApexNPCBehaviorMode.Idle);
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

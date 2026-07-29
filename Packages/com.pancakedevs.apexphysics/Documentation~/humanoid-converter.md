# Automatic Physical Humanoids

Apex Physics Engine 0.1.0 can convert an imported Unity Humanoid Avatar into a target-driven physical character.

## Character requirements

In the model import settings, set **Animation Type** to **Humanoid** and confirm that Unity reports a valid Avatar. The converter requires hips, head, both upper and lower arms, both hands, both upper and lower legs, and both feet.

The selected object can be:

- A model or prefab asset in the Project window
- A prefab instance in a scene
- A regular scene GameObject containing a Humanoid Animator

When a Project asset is selected, Apex creates a scene instance before conversion.

## Open the converter

Choose:

```text
Apex Physics Engine > Characters > Convert Selected Humanoid...
```

Select one of these modes:

```text
Active Ragdoll
Physical NPC
Physical Player
```

You can also use the direct conversion commands in the same menu.

## Generated structure

Apex creates a wrapper containing two copies of the character:

```text
<Character> Apex Physical Humanoid
├── <Character> Physical
│   ├── Visible SkinnedMeshRenderer
│   ├── Disabled Animator
│   ├── ApexActiveRagdoll
│   ├── ApexRagdollCollisionFilter
│   └── Humanoid bones
│       ├── Rigidbody
│       ├── Collider
│       ├── ConfigurableJoint
│       └── ApexRagdollBone
└── <Character> Animated Target
    ├── Enabled Humanoid Animator
    └── Hidden renderers
```

The physical character remains visible. The hidden target continues to play animation and supplies target rotations to the physical muscles.

## Automatic bone setup

The converter maps the following Humanoid bones when available:

- Hips, spine, chest, upper chest, neck, and head
- Shoulders, upper arms, lower arms, and hands
- Upper legs, lower legs, feet, and optional toes

It generates:

- Starter mass values
- Capsule or sphere colliders
- Connected ConfigurableJoints
- Starter angular limits
- Active-ragdoll target mappings
- Adjacent-bone collision exclusions
- Reusable ragdoll and player profiles

These values are intended to produce a working starting point. Models with unusual proportions or bone axes still require collider and joint-limit tuning.

## Physical NPC mode

Physical NPC conversion adds these systems to the physical hips:

```text
ApexBody
NavMeshAgent
ApexNPCNavigator
ApexNPCMotor
ApexNPCBrain
```

The NPC uses NavMesh paths for guidance while Rigidbody forces move and balance the physical hips. Existing Idle, Wander, Chase, and timed impact-retaliation behavior remains available. Strong impacts on any generated ragdoll bone can knock the character down, after which the ragdoll profile controls recovery.

Build a NavMesh before testing NPC movement.

## Physical Player mode

Physical Player conversion creates three SDK-independent tracking targets:

```text
VR Tracking Targets
├── Head Target
├── Left Hand Target
└── Right Hand Target
```

`ApexHumanoidTrackingDriver` applies those poses to the hidden target skeleton. `ApexHumanoidPlayerMotor` moves and balances the physical hips without creating a second capsule-based body.

Drive the player motor from an input adapter:

```csharp
humanoid.HumanoidPlayerMotor.SetMoveInput(move);
humanoid.HumanoidPlayerMotor.SetTurnInput(turn);
humanoid.HumanoidPlayerMotor.RequestJump();
humanoid.HumanoidPlayerMotor.SnapTurn(45f);
```

Assign real headset and controller transforms through the tracking driver:

```csharp
humanoid.TrackingDriver.SetTrackingTargets(
    headset,
    leftController,
    rightController);
```

The core package intentionally does not depend on OpenXR, XR Interaction Toolkit, Meta SDK, SteamVR, or a particular Input System action asset. Optional adapters can connect those systems later.

## Prefabs and crates

Enable **Save Converted Prefab** to save the generated character under:

```text
Assets/Apex Physics Engine/Humanoids/
```

Enable **Create Warehouse Crate** to also create an `ApexSpawnableCrate` under:

```text
Assets/Apex Physics Engine/Warehouse/Crates/
```

The physical humanoid can then be spawned by an `ApexCrateSpawner` like any other Warehouse prefab.

## Play Mode testing

Select the generated wrapper. Its custom inspector provides:

- Knock Down, Recover, and Activate controls
- Physical-player forward, stop, jump, and snap-turn controls
- Physical-NPC idle, wander, and new-destination controls
- Live state, grounding, and velocity information

## Current limitations

- Automatic collider sizes and joint limits are starter estimates.
- The first tracking layer directly targets head and hand bones; elbow prediction and full arm IK are not included yet.
- A real VR project still needs an adapter that supplies headset/controller poses and locomotion input.
- Get-up behavior currently blends muscle strength back in instead of selecting dedicated front-facing and back-facing get-up animations.
- Unity compilation and physics behavior must be validated in the target Unity 6.2 project before treating the generated values as production tuning.

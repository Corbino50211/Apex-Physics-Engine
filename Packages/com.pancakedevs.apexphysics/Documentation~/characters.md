# Apex Physical Characters

Apex Physics Engine 0.0.8 combines the planned active-ragdoll and physical-player milestones into one release.

## Physical player rig

Choose:

```text
Apex Physics Engine > Characters > Create Physical Player Rig
```

The command creates:

```text
Apex Physical Player
├── Rigidbody
├── CapsuleCollider
├── ApexBody
├── ApexPhysicalPlayerRig
├── Tracking Targets
│   ├── Head Target
│   ├── Left Hand Target
│   └── Right Hand Target
├── Physical Head
├── Physical Left Hand
└── Physical Right Hand
```

It also creates or reuses this project asset:

```text
Assets/Apex Physics Engine/Profiles/Apex Physical Player Profile.asset
```

The generated head and hand bodies use `ApexTrackedBodyPart`. They follow their targets with force and torque, ignore collisions with the owning player body, and snap back when they exceed the configured separation distance.

### Input adapters

`ApexPhysicalPlayerRig` does not read Unity Input System or XR controls directly. Drive it from an adapter by calling:

```csharp
player.SetMoveInput(move);
player.SetTurnInput(turn);
player.SetCrouch(crouching);
player.RequestJump();
```

Assign tracked transforms with:

```csharp
player.SetTrackingTargets(head, leftHand, rightHand);
```

This keeps the core compatible with desktop controls, XR controllers, AI possession, replay systems, and networking packages.

### Player behavior

The root Rigidbody provides:

- Ground movement and air control
- Braking when movement input is released
- Torque-based smooth turning
- Snap-turn commands
- Velocity-change jumping
- Manual crouching
- Optional crouching from tracked head height
- Ceiling checks before standing up
- Dynamic physical head and hand proxies

## Active ragdoll setup

Apex active ragdolls use two skeletons:

1. An animated target skeleton driven by an Animator.
2. A physical skeleton with colliders, Rigidbodies, and ConfigurableJoints.

The two skeletons must use matching local bone axes. Identical cloned skeletons are the easiest starting point.

### Configure physical bones

For each physical bone:

1. Select the bone.
2. Choose **Apex Physics Engine > Characters > Make Selected Rigidbody a Ragdoll Bone**.
3. Assign the matching animated target Transform in `ApexRagdollBone`.
4. Mark the pelvis or main body as the root bone.
5. Configure the ConfigurableJoint limits and connected body for the model.

### Configure the controller

1. Select the physical ragdoll root.
2. Choose **Apex Physics Engine > Characters > Make Selected Object an Active Ragdoll Controller**.
3. Assign the Animator from the animated target hierarchy.
4. Click **Refresh Bones**.
5. Click **Map Targets by Bone Name** when both skeletons use matching names.
6. Click **Capture Current Pose** while both skeletons are aligned.

The setup command creates or reuses:

```text
Assets/Apex Physics Engine/Profiles/Apex Ragdoll Profile.asset
```

## Ragdoll states

```text
Active
  Muscles and root following are fully enabled.

Limp
  Muscle drives and root following are disabled.

Recovering
  Muscle and root strength blend from zero back to full strength.
```

Hard impacts can automatically switch the ragdoll to Limp. When automatic recovery is enabled, the controller waits for the configured limp duration, enters Recovering, and returns to Active after the recovery blend finishes.

## Editor test controls

The `ApexActiveRagdoll` inspector includes:

- Refresh Bones
- Map Targets by Bone Name
- Capture Current Pose
- Activate
- Knock Down
- Recover

The `ApexPhysicalPlayerRig` inspector includes:

- Apply Player Profile
- Jump
- Snap turn left or right
- Crouch or stand
- Move forward
- Stop input
- Live grounding and velocity diagnostics

## Current limitations

- Apex does not automatically fit colliders and joint limits for every humanoid model. Body proportions and bone orientations differ too much for a safe universal result.
- The first recovery system blends muscles back on; it does not yet select front-facing or back-facing get-up animations.
- The physical player does not yet include a packaged Input System or XR adapter.
- Tracked head and hand bodies are physical proxies. Camera rendering and XR tracking origin correction remain adapter responsibilities.
- Unity compilation and Play Mode physics behavior must be validated in the Unity 6.2 test project before release.

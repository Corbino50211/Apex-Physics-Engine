# Animated Physical NPCs

Apex 0.2.0 replaces the experimental foot-tether and torso-harness stack with one coordinated physical-character controller.

## Runtime architecture

- **Support body:** authoritative Rigidbody used for navigation, ground collision, and turning.
- **Animated target:** hidden Humanoid skeleton that supplies walking and get-up poses.
- **Physical body:** visible articulated Rigidbody skeleton driven toward the animated target.

## States

### Active

The physical hips are rigidly connected to the support body. The support body follows NavMesh steering, while the hidden animated target follows the support hips anchor. The active-ragdoll muscles pull the visible body into the current animated pose.

The controller sends these optional Animator parameters when they exist:

- `Speed` — normalized locomotion speed.
- `Moving` — true while moving.
- `Grounded` — support-body grounded state.
- `Ragdoll` — true while released.
- `GetUpFront` — trigger used for face-down recovery.
- `GetUpBack` — trigger used for face-up recovery.

When the target Animator has no controller or no `Speed` float parameter, Apex uses a Humanoid-muscle procedural gait so the legs and arms still move.

### Ragdoll

A hard external Rigidbody impact can switch the active ragdoll to Limp. Apex releases every root constraint, disables the support colliders, stops navigation, and leaves the articulated body fully physical.

### Getting Up

After the ragdoll profile's Limp Duration, Apex places the support body beneath the fallen hips, selects the front or back get-up trigger, reconnects the physical root, and raises the body while muscle strength blends back. Navigation resumes only when the active-ragdoll state returns to Active.

## Upgrade an existing NPC

Exit Play Mode, select the generated Apex Physical NPC, then run:

`Apex Physics Engine > Characters > Rebuild Selected NPC Physical Controller`

The repair removes legacy locoball foot tethers and the torso harness, rebuilds the support body, and installs the unified controller.

## Custom animations

For authored locomotion and recovery, assign an Animator Controller to the hidden target Animator and use the default parameter names above, or edit the names on `ApexHumanoidPhysicalController`.

Without custom clips, Apex uses procedural walking and a physics-driven recovery blend.

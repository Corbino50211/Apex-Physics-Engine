# Supported Physical Humanoids

Apex Physics Engine uses a simplified central support body plus an articulated visible skeleton for converted physical NPCs.

## Why a pure ragdoll collapses

A fully dynamic humanoid cannot reliably stand and navigate from joint muscles alone. Apex therefore separates stable locomotion from visible articulated reaction physics.

## Supported-rig structure

```text
Apex Physical Humanoid
├── Apex Humanoid Support Body
│   ├── Rigidbody at the physical hips
│   ├── torso BoxCollider
│   ├── lower-body CapsuleCollider
│   └── Apex Humanoid Locoball
│       ├── floor-contact SphereCollider
│       ├── Left Foot Anchor
│       └── Right Foot Anchor
├── Visible Physical Character
│   ├── articulated active-ragdoll skeleton
│   ├── left-foot spring tether
│   └── right-foot spring tether
└── Hidden Animated Target
```

The support body owns root locomotion and upright stability. The visible spine, arms, hands, legs, feet, neck, and head remain physics-driven.

This is an original Apex implementation inspired by the general simplified-body approach visible in physics-character rigs. It does not use proprietary Marrow source code.

## Locoball and support placement

The support Rigidbody origin is rebuilt at the physical hips instead of being left near scene Y = 0. Its torso and lower-body colliders are regenerated from the actual hips, chest, sole, and detected floor positions.

The old embedded ground sphere is disabled and replaced by a child locoball with its own transform at floor height. This makes the floor contact easy to inspect and tune.

## Foot tethers

Each physical foot receives an `ApexHumanoidFootTether` connected to a left or right anchor on the locoball.

While Active or Recovering:

- The tethers keep the feet within a limited distance of the locoball.
- Knees and ankles remain articulated through their normal ragdoll joints.
- The legs cannot stretch or fly far away from the locomotion core.

While Limp:

- Tether springs and damping are removed.
- Maximum tether distance expands so the body can fall naturally.
- Recovery rebuilds the locoball and brings the feet back into a controllable range.

## Knockdown filtering

Converted humanoids ignore startup and internal settling impacts. By default, a knockdown requires an external, non-kinematic Rigidbody collision.

This prevents the following from immediately forcing Limp:

- Moving or rebuilding the support body
- Touching the static floor
- Colliding with the NPC's own support body or locoball
- Generated parts settling during the first second of Play Mode

Thrown dynamic bodies can still trigger real knockdowns.

## Upgrade an existing NPC

1. Update Apex Physics Engine.
2. Exit Play Mode.
3. Select the generated physical NPC or any child under it.
4. Run **Apex Physics Engine > Characters > Rebuild Selected NPC Locoball**.
5. Save the scene.
6. Build the NavMesh if needed.
7. Enter Play Mode.

Deleting and reconverting the humanoid is not required.

## New conversions

New **Physical NPC** conversions install the support body, locoball, anchors, and foot tethers automatically.

## Tuning

Select the generated wrapper and tune `ApexHumanoidLocoballRig`:

- Locoball radius
- Floor clearance
- Left/right foot spread
- Tether spring and damping
- Maximum active tether distance
- Limp slack distance
- Impact arming delay

Select each generated `ApexHumanoidFootTether` for per-foot inspection.

## Current limitations

- The support body and locoball still own locomotion; procedural stepping is not implemented yet.
- Feet are softly constrained rather than planted with full inverse-kinematics foot placement.
- Dedicated slope adaptation, stairs, obstacle climbing, and front/back get-up animation selection remain future work.
- Unity compilation and Play Mode tuning must be confirmed in the target Unity 6.2 project.

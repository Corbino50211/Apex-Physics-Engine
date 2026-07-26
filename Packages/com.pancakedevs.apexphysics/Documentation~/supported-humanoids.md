# Supported Physical Humanoids

Apex Physics Engine 0.1.1 adds a stable central support body to converted physical NPCs.

## Why the old NPC collapsed

The 0.1.0 converter made every humanoid bone dynamic and asked the physical hips to move, balance, and carry the complete articulated body. That produces a genuine ragdoll, but it does not provide enough stable structure for ordinary standing and walking.

## Supported-rig structure

Apex now creates this hidden physics structure:

```text
Apex Physical Humanoid
├── Apex Humanoid Support Body
│   ├── Rigidbody
│   ├── ApexBody
│   ├── torso BoxCollider
│   ├── lower-body CapsuleCollider
│   └── ground-contact SphereCollider
├── Visible Physical Character
│   └── articulated physical skeleton
└── Hidden Animated Target
```

The support body is deliberately simple and stable. The visible body remains fully articulated and follows the hidden animated target through active-ragdoll muscles.

This is an original Apex implementation inspired by the general simplified-body approach visible in physics-character rigs. It does not use proprietary Marrow source code.

## Active movement

While the active ragdoll is Active:

- The support body remains upright.
- The NavMesh navigator continues planning paths.
- `ApexHumanoidSupportRig` moves and turns the support body.
- The visible hips are connected to the support body.
- The torso and limbs remain physical and can collide with the environment.
- The old hips-based `ApexNPCMotor` is disabled so the two motors do not fight.

## Knockdown and recovery

When a sufficiently hard impact changes the ragdoll to Limp:

- NPC support movement stops.
- Upright constraints are released.
- The support core and articulated body can fall together.
- Active-ragdoll muscles are disabled by the existing ragdoll state system.

When recovery starts:

- The support body is reset to an upright orientation near the physical hips.
- Upright constraints return.
- Movement strength blends back with the active-ragdoll recovery progress.
- Normal NPC navigation resumes when the character becomes Active.

## Upgrade an existing converted NPC

1. Update Apex Physics Engine to 0.1.1.
2. Select the generated `Apex Physical Humanoid` wrapper.
3. In the `ApexPhysicalHumanoid` inspector, find **Supported Physics Core**.
4. Click **Install Supported Physics Core**.
5. Build the NavMesh if needed.
6. Enter Play Mode.

Existing converted NPCs also install the support body automatically when Play Mode begins, but installing it in Edit Mode makes the generated shape visible and editable before testing.

## New conversions

New **Physical NPC** conversions install the support body automatically. Active-ragdoll-only conversions are unchanged. Physical-player support remains a separate later tuning pass because VR players require headset-height, climbing, and body-calibration rules that differ from autonomous NPCs.

## Tuning

Select the wrapper and expand `ApexHumanoidSupportRig` to tune:

- Movement speed
- Acceleration and braking
- Maximum movement force
- Turning responsiveness
- Maximum turning torque
- Support body height and radius
- Torso width, depth, and height
- Support mass

The generated proportions use the Humanoid Avatar's head and feet when available, with renderer bounds as a fallback.

## Current limitations

- The support body is hidden but still contributes the main environmental collision volume.
- Feet and limbs remain physical, but the support body—not footstep simulation—currently owns locomotion.
- Dedicated stepping, slope adaptation, procedural foot placement, and obstacle climbing are future systems.
- Recovery currently resets the support core upright before muscles blend back; dedicated front/back get-up animation selection remains future work.
- Unity compilation and Play Mode tuning must be confirmed in the target Unity 6.2 project.

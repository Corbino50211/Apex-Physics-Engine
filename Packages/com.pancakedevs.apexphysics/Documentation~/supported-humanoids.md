# Supported Physical Humanoids

Apex Physics Engine 0.1.4 uses a stable central support body for converted physical NPCs.

## Why the old NPC collapsed

The first humanoid converter made every bone dynamic and asked the physical hips to move, balance, and carry the complete articulated body. That creates a genuine ragdoll, but it does not provide enough stable structure for ordinary standing and walking.

## Supported-rig structure

Apex creates this hidden physics structure:

```text
Apex Physical Humanoid
├── Apex Humanoid Support Body
│   ├── Rigidbody
│   ├── ApexBody
│   ├── torso BoxCollider
│   ├── lower-body CapsuleCollider
│   ├── ground-contact SphereCollider
│   └── Hips Anchor
├── Visible Physical Character
│   └── articulated physical skeleton
└── Hidden Animated Target
```

The support body is deliberately simple and stable. The visible torso and limbs remain articulated and follow the hidden animated target through active-ragdoll muscles.

This is an original Apex implementation inspired by the general simplified-body approach visible in physics-character rigs. It does not use proprietary Marrow source code.

## Supported root constraint

The hips are different from ordinary ragdoll limbs. Their ConfigurableJoint connects to the separate support Rigidbody rather than to the hips bone's normal transform parent.

Apex therefore does not run the normal local-space muscle equation on support-connected hips. The support body owns root position and rotation through a rigid hips constraint. The spine, arms, hands, legs, feet, neck, and head continue using physical muscle drives.

This prevents an invalid hips target rotation from folding the complete body around the support core.

## Active movement

While the active ragdoll is Active:

- The support body remains upright.
- The NavMesh navigator continues planning paths.
- `ApexHumanoidSupportRig` moves and turns the support body.
- The physical hips remain rigidly attached to the support body.
- The torso and limbs remain physical and can collide with the environment.
- The old hips-based `ApexNPCMotor` is disabled so the two motors do not fight.

## Knockdown and recovery

When a sufficiently hard impact changes the ragdoll to Limp:

- NPC support movement stops.
- Upright constraints on the support Rigidbody are released.
- The support core and articulated body can fall together.
- Active-ragdoll limb muscles are disabled by the ragdoll state system.

When recovery starts:

- The support body is reset to an upright orientation.
- Upright constraints return.
- Limb strength blends back with active-ragdoll recovery progress.
- Normal NPC navigation resumes when the character becomes Active.

## Upgrade or repair an existing NPC

1. Update Apex Physics Engine to 0.1.4.
2. Exit Play Mode.
3. Select the generated `Apex Physical Humanoid` wrapper.
4. Find **Supported Physics Core** in the inspector.
5. Click **Rebuild and Calibrate Standing Pose**.
6. Save the scene.
7. Enter Play Mode.

Calibration is intentionally unavailable during Play Mode so a fallen or deformed runtime pose cannot become the saved rest pose.

## New conversions

New **Physical NPC** conversions install the support body automatically. Active-ragdoll-only conversions are unchanged. Physical-player support remains a separate tuning path because VR players require headset height, climbing, and body-calibration behavior that differs from autonomous NPCs.

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

- The support body is hidden but still provides the main environmental collision volume.
- Feet and limbs remain physical, but the support body—not footstep simulation—currently owns locomotion.
- Dedicated stepping, slope adaptation, procedural foot placement, and obstacle climbing are future systems.
- Recovery currently resets the support core upright before muscles blend back; dedicated front/back get-up animation selection remains future work.
- Automatic limb joint axes and angular limits are starter values and may still need model-specific tuning.
- Unity compilation and Play Mode tuning must be confirmed in the target Unity 6.2 project.

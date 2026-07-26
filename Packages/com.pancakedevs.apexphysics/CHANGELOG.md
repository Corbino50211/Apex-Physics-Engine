# Changelog

All notable changes to Apex Physics Engine will be documented here.

## [0.1.4] - 2026-07-26

### Fixed

- Supported physical NPC hips are no longer driven by the ordinary local-space ragdoll muscle equation after being connected to the separate support Rigidbody.
- Fixed support-connected hips applying an invalid target rotation and folding the spine and legs around the central support core.
- Root position and rotation are now owned by the simplified support body, while the torso and limbs continue using active-ragdoll muscles.
- The support-connected hips joint now uses a rigid root constraint with zero secondary muscle drive.

### Changed

- Standing-pose calibration remains disabled during Play Mode so a deformed runtime pose cannot become the saved rest pose.

### Notes

- Existing 0.1.3 NPCs can be repaired in place. Update, exit Play Mode, run **Rebuild and Calibrate Standing Pose**, save the scene, and test again.
- Unity compilation and Play Mode tuning still require validation inside the Unity 6.2 test project before the draft release is merged.

## [0.1.3] - 2026-07-26

### Fixed

- `ApexHumanoidTargetRootDriver` now preserves the Humanoid hips bone's authored position and rotation offsets instead of forcing the target hips to exactly match the simplified support transform.
- Fixed converted NPCs being pulled into arched, bridge-like, or sideways poses when a model's hips bone uses a non-identity bind rotation.
- Existing generated NPCs can restore the hidden target hips from the upright physical bind pose before recalibrating the support-relative offset.
- Standing-pose repair now refreshes the ragdoll bone list and recaptures muscle rest rotations after target-root calibration.

### Changed

- Replaced **Rebuild and Stand Up** with **Rebuild and Calibrate Standing Pose** in the generated humanoid inspector.
- Standing-pose calibration is intentionally Edit Mode only so a fallen runtime pose cannot accidentally become the new muscle rest pose.

### Notes

- Existing 0.1.2 NPCs do not need to be deleted or reconverted. Update the package, exit Play Mode, select the generated humanoid root, and run the calibration command once.
- Unity compilation and Play Mode tuning still require validation inside the Unity 6.2 test project before the draft release is merged.

## [0.1.2] - 2026-07-26

### Fixed

- Supported humanoids no longer place the central support collider directly at the hips, which previously pushed the lower capsule deeply through the floor on normal body proportions.
- Support-body placement now detects external ground or the generated foot-sole height and aligns the lowest support collider above that plane with a small clearance.
- Added a persistent hips anchor so floor-aligning the support body does not move the hidden animated target away from the physical hips.
- Hips joint anchors are now configured explicitly instead of relying on automatic connected-anchor recalculation.
- Converted physical NPCs disable ragdoll self-collision by default to prevent overlapping automatically generated torso, shoulder, and leg colliders from launching the character at startup.
- Recovery and manual snapping now restore the saved standing hips offset instead of centering the support body on a fallen hips position.

### Added

- **Rebuild and Stand Up** inspector repair command for converted physical NPCs.
- Live hips-anchor reference in the supported physics core inspector.

### Notes

- Existing 0.1.1 NPCs can be repaired in place; deleting and reconverting the character is not required.
- Unity compilation and Play Mode tuning still require validation inside the Unity 6.2 test project before the draft release is merged.

## [0.1.1] - 2026-07-26

### Added

- `ApexHumanoidSupportRig`, a hidden Marrow-inspired central physics body for converted physical NPCs.
- A composite support shape using a torso box, lower-body capsule, and ground-contact sphere.
- Automatic support-body installation for newly converted and existing `ApexPhysicalHumanoid` NPCs.
- A driven hips connection that keeps the visible articulated skeleton attached to the stable support body.
- Support-body NavMesh movement while preserving the existing NPC navigator, brain, targets, and behavior settings.
- Knockdown integration that releases upright constraints while Limp and restores the supported core during recovery.
- Internal collision filtering between the support body and visible physical skeleton.
- Inspector controls to install, rebuild, inspect, and snap the supported physics core.

### Changed

- Converted physical NPCs no longer rely on the hips-only `ApexNPCMotor` to carry the entire articulated body.
- The legacy hips motor remains installed for compatibility but is disabled while the support rig drives locomotion.
- The hidden animated target root now follows the support body instead of unstable physical hips.

### Notes

- Version 0.1.1 is an original supported-ragdoll architecture inspired by the general simplified-body approach shown in physics-character rigs; it is not an implementation of proprietary Marrow code.
- Support-body proportions are generated from humanoid height and remain editable per character.
- Unity compilation and Play Mode tuning still require validation inside the Unity 6.2 test project before the draft release is merged.

## [0.1.0] - 2026-07-26

### Added

- Automatic conversion of imported Humanoid Avatar characters into Apex physical humanoids.
- Physical NPC, physical player, and active-ragdoll-only conversion modes.
- A visible physical character paired with a hidden animated target duplicate.
- Automatic Humanoid bone mapping for hips, torso, head, arms, hands, legs, feet, shoulders, neck, and optional toes.
- Starter Rigidbody masses, colliders, ConfigurableJoints, joint limits, connected bodies, muscle mappings, and adjacent-bone collision filtering.
- `ApexPhysicalHumanoid` generated-rig descriptor and runtime diagnostics.
- `ApexHumanoidTargetRootDriver` for keeping the hidden target hips aligned with the physical hips.
- `ApexHumanoidTrackingDriver` with generated VR head and hand targets.
- `ApexHumanoidPlayerMotor` for input-agnostic full-body movement, balance, turning, jumping, knockdown control loss, and recovery blending without a second capsule body.
- Automatic physical NPC setup using the existing NavMesh navigator, Rigidbody motor, and Idle/Wander/Chase brain.
- Optional saving of converted humanoids as prefabs and automatic Warehouse crate creation.
- A dedicated converter window, direct conversion menu commands, validation messages, and Play Mode test controls.

### Changed

- `ApexActiveRagdoll` now exposes converter-friendly configuration for target Animators, root bones, and root-following rules.
- `ApexRagdollBone` now exposes per-bone muscle multiplier configuration.

### Notes

- Generated collider sizes and joint limits are safe starter values, not final tuning for every body shape or bone orientation.
- The physical-player conversion creates SDK-independent tracking targets. An OpenXR/XR Interaction Toolkit adapter still needs to assign real headset and controller poses.
- Direct hand-bone targeting is the first VR target layer; elbow prediction, arm IK, climbing, and hand interaction integration remain later milestones.
- Unity compilation and Play Mode behavior still require validation inside the Unity 6.2 test project before the draft release is merged.

## [0.0.9] - 2026-07-26

### Added

- `ApexCrate` base assets with stable barcodes, titles, descriptions, tags, and crate categories.
- `ApexSpawnableCrate` prefab assets for reusable runtime content.
- `ApexPallet` assets for grouping crates that should be registered together.
- `ApexWarehouse` runtime registry with persistent-scene support and barcode resolution.
- Direct and barcode-based crate spawning APIs.
- `ApexCrateSpawner` scene markers with Awake, Start, End Of Frame, and Manual timing.
- Multi-instance spawning, position stepping, optional parenting, spawn-once rules, and runtime clearing.
- `ApexCrateInstance` identity metadata for future save, reset, networking, and mod systems.
- One-click crate creation from selected prefabs, pallet creation, warehouse creation, and spawner placement.
- Warehouse, crate, and spawner inspectors with runtime diagnostics.
- Warehouse asset validation for duplicate barcodes and missing prefabs.

### Notes

- Version 0.0.9 uses direct Unity prefab references. Addressables, asset bundles, asynchronous mod pallets, and network-spawn adapters remain future warehouse layers.
- Unity compilation and level-load spawning require validation in the Unity 6.2 test project before the draft release is merged.

## [0.0.8] - 2026-07-26

### Added

- Combined the planned 0.0.7 active-ragdoll work and 0.0.8 physical-player work into one **Physical Characters** release.
- `ApexRagdollProfile` for reusable muscle, root-following, knockdown, and recovery tuning.
- `ApexRagdollBone` for ConfigurableJoint muscle drives toward matching animated target bones.
- `ApexActiveRagdoll` with Active, Limp, and Recovering states, impact knockdowns, automatic recovery delays, and blended muscle restoration.
- Root position and rotation following for target-driven physical skeletons.
- Bone-name target mapping, pose capture, refresh, knockdown, and recovery editor controls.
- `ApexPhysicalPlayerProfile` for movement, turning, jumping, crouching, body sizing, and tracked-part tuning.
- `ApexPhysicalPlayerRig`, an input-agnostic Rigidbody player motor with ground movement, air control, braking, torque turning, jumping, tracked-height crouching, and ceiling checks.
- `ApexTrackedBodyPart` for force-driven physical head and hand proxies with distance recovery and owner-collision filtering.
- One-click physical-player rig generation with tracking targets and physical head/hand bodies.
- Character setup commands and runtime inspectors under **Apex Physics Engine > Characters**.
- Ragdoll and physical-player profile creation commands.

### Notes

- Active ragdolls require separate animated-target and physical skeleton hierarchies with matching local bone axes.
- Automatic target mapping uses matching bone names; collider and joint fitting remain model-specific.
- The physical player core intentionally does not depend on Input System or XR packages. Adapters call its public movement and tracking API.
- Unity compilation and Play Mode physics behavior require validation in the Unity 6.2 test project before the draft release is merged.

## [0.0.6] - 2026-07-26

### Added

- Editor-only `ApexLightingBaker` workflow for automatic lightmap generation.
- Optional bake-before-play interception with automatic Play Mode resume.
- Scene dirty detection based on baked lights, GI renderers, meshes, terrains, reflection probes, Render Settings, and Lighting Settings.
- Missing-lighting-data detection so unbaked scenes are generated automatically.
- Preview, Production, and Custom bake-quality presets.
- Reflection-probe baking for enabled Baked-mode probes.
- Bake progress, cancellation, clear-data, mark-dirty, and manual bake controls.
- Persistent project-wide lighting settings with per-scene last-bake hashes and timestamps.
- Dedicated **Apex Physics Engine > Lighting** top-bar menu and Lighting Baker window.

### Notes

- Lighting generation is an Editor workflow. Standalone builds load lighting data baked in the Editor.
- Unity compilation and Play Mode behavior require validation in a Unity 6 project before the draft release is merged.

## [0.0.5] - 2026-07-26

### Added

- `ApexNPCBrain` with selectable Idle, Wander, and Chase behavior modes.
- Random NavMesh wandering around a remembered home position.
- Configurable wander radius, wait time, NavMesh sample distance, and sample attempts.
- Hard-impact retaliation using `ApexBody.Impacted` collision data.
- Configurable impact speed and impulse thresholds.
- Timed chase behavior that returns to Wander after 10 seconds by default.
- Repeated hard impacts refresh the chase timer and update the chase target.
- Runtime behavior inspector controls and diagnostics.
- Missing-brain warning and one-click repair button for existing NPCs.

### Changed

- **Make Selected Object a Basic NPC** now also adds `ApexNPCBrain`.

## [0.0.4] - 2026-07-26

### Added

- `ApexNPCMotor` for force-driven Rigidbody movement along navigator paths.
- Configurable acceleration, braking, movement speed, turn torque, and upright stabilization.
- Automatic Rigidbody interpolation and continuous collision setup for basic NPCs.
- Missing-motor warning and one-click repair button in the NPC navigator inspector.
- Live requested-velocity and Rigidbody-velocity diagnostics.

### Changed

- **Make Selected Object a Basic NPC** now adds Collider, Rigidbody, `ApexBody`, `NavMeshAgent`, `ApexNPCNavigator`, and `ApexNPCMotor`.

## [0.0.3] - 2026-07-26

### Added

- Unity AI Navigation 2.0.9 dependency for Unity 6.
- `ApexNavMeshAutoBaker` with manual, before-play, startup, and runtime-interval build modes.
- Dirty-state tracking so navigation rebuilds can be requested without baking every frame.
- `ApexNPCNavigator` for path planning, steering output, destination sampling, repathing, and arrival events.
- Physics-friendly NavMeshAgent configuration that does not directly move or rotate the NPC transform.
- Inspector controls for building, clearing, and marking NavMesh surfaces dirty.
- One-click editor commands for creating navigation surfaces and NPC navigators.
- Dedicated **Apex Physics Engine** top-bar menu for bodies, grabbing, navigation, and profile creation.

### Changed

- Moved Apex setup commands out of Unity's GameObject menu and into the dedicated Apex top-bar menu.

## [0.0.2] - 2026-07-26

### Added

- `ApexGrabbable` with free grabbing, authored grab points, multi-hand limits, and stealing rules.
- `ApexGrabber` with force-driven position and rotation following.
- Reusable `ApexGrabProfile` strength, damping, break-distance, mass, and throw settings.
- Left, right, and universal hand filtering.
- Smoothed hand velocity sampling and release/throw velocity transfer.
- One-click editor commands for creating grabbables, grabbers, and grab points.
- Play-mode grab and release controls in the `ApexGrabber` inspector.

## [0.0.1] - 2026-07-26

### Added

- Initial Unity Package Manager manifest.
- `ApexBody` core Rigidbody wrapper.
- Reusable `ApexPhysicsProfile` assets.
- Universal `ApexImpactInfo` collision data.
- Custom `ApexBody` inspector with profile and runtime controls.
- Initial Physics Lab sample instructions.

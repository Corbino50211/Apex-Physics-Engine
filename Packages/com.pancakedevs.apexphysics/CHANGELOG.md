# Changelog

All notable changes to Apex Physics Engine will be documented here.

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
- Runtime and editor assembly definitions.
- `ApexBody` core Rigidbody wrapper.
- Reusable `ApexPhysicsProfile` assets.
- Universal `ApexImpactInfo` collision data.
- Custom `ApexBody` inspector with profile and runtime controls.
- Initial Physics Lab sample instructions.

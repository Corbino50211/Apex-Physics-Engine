# Changelog

All notable changes to Apex Physics Engine will be documented here.

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

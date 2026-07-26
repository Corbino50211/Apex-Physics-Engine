# Changelog

All notable changes to Apex Physics Engine will be documented here.

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

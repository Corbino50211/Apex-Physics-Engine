# Changelog

All notable changes to Apex Physics Engine will be documented here.

## [0.3.2] - 2026-07-28

### Added

- Apex Void Preview shader with animated darkness, fresnel edge glow, and procedural scan-grid detail.
- Rotatable void-model previews in Spawnable Crate and Crate Spawner inspectors.
- Scene-view crate previews at every `ApexCrateSpawner` marker without instantiating gameplay prefabs.
- Per-crate preview prefab, material, position, rotation, and scale overrides.
- **Create Editable Void Material** for saving a customizable material beside a crate asset.
- Preview visibility and selected-only controls on crate spawners.

### Changed

- Spawnable crates use their normal spawn prefab as the preview model unless a simplified preview prefab is assigned.
- Multi-copy spawners display every configured Spawn Count and Position Step in Edit Mode.

### Notes

- The package default is a dependency-free ShaderLab implementation so Apex does not force Shader Graph into every project.
- The Preview Material field accepts any custom material, including a project-authored Shader Graph material.
- Unity compilation and Scene-view rendering still require confirmation in the Unity 6.2 test project.

## [0.3.1] - 2026-07-28

### Fixed

- Repositioned the PC character motor Rigidbody pivot from floor level to the humanoid hips.
- Kept the motor capsule bottom aligned with the floor while embedding the collider around the pelvis and lower torso.
- Automatically refits the motor after get-up recovery.

## [0.3.0] - 2026-07-29

### Added

- `ApexPCPhysicalCharacter`, one PC-first component that owns locomotion, animated pose following, impact ragdoll, and get-up recovery.
- A generated `Apex PC Character Motor` capsule Rigidbody as the only authoritative active locomotion body.
- `ApexPCProceduralGait` for automatic Humanoid walking when no Animator Controller is assigned.
- A simplified PC-only character builder and unified runtime inspector.
- **Apex Physics Engine > Characters > Rebuild Selected PC Physical Character** for upgrading older generated NPCs.

### Changed

- The 0.3.x authoring workflow only creates PC physical NPCs. VR player generation and tracked-hand setup are postponed.
- Active characters now follow the animated target exactly for stability instead of relying on competing standing springs.
- Hard impacts release the complete generated skeleton into dynamic ragdoll simulation.
- Recovery places the motor beneath the fallen body, triggers optional front/back get-up animations, and blends the body back to the target pose.
- Generated ragdolls ignore self-collisions by default to prevent overlapping automatic colliders from exploding.

### Removed from new character setup

- Locoball foot tethers.
- Torso harness forces.
- The old supported-root and animated-physical controller stack.
- Physical-player and VR choices from the character builder.

### Notes

- Legacy character components remain in the package so older serialized prefabs can still load.
- Unity compilation and Play Mode behavior still require validation in the Unity 6.2 test project.

## [0.1.5] - 2026-07-26

### Added

- `ApexHumanoidLocoballRig`, a floor-aligned locomotion sphere generated beneath supported physical NPCs.
- Explicit left and right locoball foot anchors.
- `ApexHumanoidFootTether` spring constraints that keep articulated feet near the locomotion core without hard-locking the knees.
- Limp-state tether slack so the complete humanoid can still ragdoll naturally after a real knockdown.
- **Apex Physics Engine > Characters > Rebuild Selected NPC Locoball** for upgrading existing generated NPCs in Edit Mode.
- Automatic locoball installation for new and existing converted physical NPCs.

### Fixed

- The simplified support Rigidbody is now rebased around the physical hips instead of leaving its generated collision geometry effectively centered near scene Y = 0.
- Torso and lower-body support colliders are regenerated from actual hips, chest, sole, and ground positions.
- The old embedded ground sphere is replaced by a dedicated child locoball at the detected floor height.
- Converted humanoids ignore startup, static-floor, kinematic, and internal generated-body impacts when deciding whether to enter Limp.
- Manual support-body repositioning and initial physics settling no longer count as real knockdown hits.

### Changed

- Real knockdowns on converted humanoids now require a dynamic external Rigidbody impact by default.
- Foot tethers are strong while Active or Recovering and become slack while Limp.

### Notes

- Existing NPCs do not need to be deleted. Update the package, exit Play Mode, select the generated physical NPC, and run **Rebuild Selected NPC Locoball** once.
- Unity compilation and Play Mode tuning still require validation inside the Unity 6.2 test project before the draft release is merged.

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

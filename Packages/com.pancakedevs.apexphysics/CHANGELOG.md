# Changelog

All notable changes to Apex Physics Engine will be documented here.

## [0.3.8] - 2026-07-28

### Added

- Natural procedural arm posing that lowers imported T-pose arms into a relaxed idle stance.
- Opposing arm swing while the motor is moving.
- A final forward/outward knee-pole correction after planted-foot IK.
- Smoothed locomotion-speed blending so idle and walking transitions do not snap.

### Changed

- Procedural leg swing is reduced because planted-foot IK now owns most lower-body motion.
- The gait component executes between the planted-foot solver and the visible physical-body pose copy.
- Existing procedural-gait components automatically migrate to the new natural-pose defaults.

### Fixed

- NPCs no longer remain in a full T-pose while idle or walking without an Animator Controller.
- Knees are directed forward with a slight outward bias instead of collapsing inward or crossing.
- The lower body no longer relies on the imported bind-pose knee direction during locomotion.

### Notes

- Existing NPCs should be rebuilt once after updating.
- Authored Animator Controllers still override the procedural fallback.
- Unity compilation and Play Mode behavior still require confirmation in the Unity 6.2 Windows test project.

## [0.3.7] - 2026-07-28

### Fixed

- Added per-foot visual sole calibration from the generated physical foot collider.
- Shoes and boots that extend below the Humanoid foot bone now receive enough lift to rest their visible sole on the floor.
- Existing 0.3.6 characters automatically migrate to the new sole-clearance defaults.

### Changed

- Maximum supported visual sole depth is increased to `0.12` metres for oversized footwear and stylized models.
- Foot targets combine leg-proportion estimation, physical foot-collider depth, and a small visual contact lift.

### Notes

- Existing NPCs should be rebuilt once after updating.
- Unity compilation and Play Mode behavior still require confirmation in the Unity 6.2 Windows test project.

## [0.3.6] - 2026-07-28

### Fixed

- Replaced the old sole-height calculation that incorrectly treated the NPC's existing air gap as part of the shoe.
- Added automatic hips-height calibration from the humanoid's actual leg geometry and sampled floor height.
- The hidden hips anchor now lowers inside the motor when needed so planted feet can physically reach the floor.
- Reduced trailing legs by using shorter, earlier steps with velocity-based forward lead.
- Allowed the opposite leg to begin its next step before the current step completely finishes.

### Changed

- Default planted-step distance is reduced from `0.28` to `0.18` metres.
- Default step duration is reduced from `0.24` to `0.17` seconds.
- Foot targets use a model-based sole estimate plus a small configurable clearance.
- Hips-height correction runs before the two-bone leg solve and before the visible body copies the hidden target pose.

### Notes

- Existing NPCs should be rebuilt once after updating so the current PC physical-character modules are refreshed.
- Unity compilation and Play Mode behavior still require validation in the Unity 6.2 Windows test project.

## [0.3.5] - 2026-07-28

### Fixed

- Removed the updater compile error caused by the ambiguous `PackageInfo` type in Unity 6.
- The installed-version lookup now explicitly uses `UnityEditor.PackageManager.PackageInfo`.

## [0.3.4] - 2026-07-28

### Added

- **Apex Physics Engine > Updates > Check for Updates...** top-level editor command.
- `ApexPackageUpdaterWindow` showing installed version, latest version, update status, and release branch.
- Automatic update checks when Unity opens, enabled by default with a 12-hour check interval.
- Optional automatic installation after an update is detected.
- Exact-commit Unity Package Manager installation so an update cannot reuse an older cached revision.
- **Reinstall Latest Revision** and **Copy Package URL** recovery actions.

### Changed

- User-started update checks offer to install a newer package immediately.
- Update installation is handled entirely through Unity Package Manager and can trigger the normal script reload.

### Notes

- Version 0.3.4 must be installed once through the normal Git package URL. Later package versions can be installed from the Apex updater window.
- Automatic installation is disabled by default. Automatic checking is enabled by default.
- The updater reads the public PancakeDevs GitHub release branch and does not require a GitHub token.
- Unity compilation and a complete self-update cycle still require confirmation in the Unity 6.2 Windows test project.

## [0.3.3] - 2026-07-28

### Added

- `ApexPCFootPlanting` for alternating world-space planted steps on the hidden Humanoid target.
- Two-bone leg solving that keeps feet near sampled ground while the motor moves.
- `ApexPCNavigationDriver` for projecting NavMesh planning beneath the authoritative PC motor.
- Automatic invalid, partial, and stuck-path retries.
- `ApexPCRagdollMomentum` for transferring motor linear velocity, angular velocity, and per-bone point velocity into ragdoll.
- Main PC character inspector diagnostics for foot readiness, NavMesh binding, path status, destination state, and remaining distance.

### Changed

- The procedural gait now receives a grounded foot-planting pass before the visible physical body copies the target pose.
- Existing Humanoid-root navigation is bridged to the generated `Apex PC Character Motor` instead of relying on the floating hips position.
- The manual **Knock Down** test now preserves current walking and turning momentum.
- **Rebuild Selected PC Physical Character** installs the motor fitter, procedural gait, foot planting, navigation bridge, and momentum handoff together.

### Fixed

- NPC feet no longer remain curled upward solely from the procedural leg-swing fallback.
- NPCs with valid destinations no longer remain asleep indefinitely until struck.
- Invalid or partial wander paths are retried instead of leaving the brain waiting forever.

### Notes

- Active characters remain kinematic target-followers for stability; planted stepping makes the locomotion pose grounded, but fully force-driven standing legs remain a later milestone.
- Unity compilation and Play Mode behavior still require validation in the Unity 6.2 Windows test project.

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

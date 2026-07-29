# PC Foot Orientation

Apex Physics Engine 0.3.11 adds a final forward-aware foot-orientation correction for procedural PC characters.

## Why it exists

Humanoid avatars do not always use the same local foot-bone axes. Rotating around an assumed local X axis can twist a shoe sideways, and using the current toe projection can preserve an already incorrect direction.

The 0.3.11 correction runs after planted-foot IK and procedural gait posing but before the visible physical body copies the target pose. It uses the authoritative PC motor's horizontal forward direction and remembers whether each avatar's toe mapping initially points with or against that direction.

## What it changes

- Final foot rotation only.
- A small planted toe pitch.
- A small additional pitch while moving.

## What it does not change

- Foot collider placement.
- Sole clearance.
- Planted foot target positions.
- Leg reach.
- Motor placement.
- NavMesh movement.

## Existing and spawned characters

A lightweight runtime bootstrap automatically installs the correction on existing generated PC characters and characters spawned later during play. No rebuild is required solely for the 0.3.11 foot-direction fix.

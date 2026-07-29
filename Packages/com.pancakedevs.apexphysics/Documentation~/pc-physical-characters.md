# PC Physical Characters

Apex 0.3.x is intentionally PC-first. VR player generation, headset targets, tracked hands, and XR adapters are postponed until the desktop physical NPC system is stable.

## Build a character

1. Import a character and set **Rig > Animation Type** to **Humanoid**.
2. Confirm the Avatar is valid and click **Apply**.
3. Select the character prefab or scene object.
4. Choose **Apex Physics Engine > Characters > Build PC Physical Character...**.
5. Click **Build PC Physical NPC**.
6. Build the NavMesh and enter Play Mode.

The builder creates one user-facing `ApexPCPhysicalCharacter` component. Generated internals include:

- A hidden animated target.
- A visible collider-equipped physical skeleton.
- One capsule Rigidbody named `Apex PC Character Motor`.
- NavMesh path planning and NPC behavior.
- Motor-projected path planning through `ApexPCNavigationDriver`.
- Procedural Humanoid walking when no Animator Controller is assigned.
- World-space planted footsteps through `ApexPCFootPlanting`.
- Impact-triggered ragdoll release.
- Momentum transfer from the motor into every released ragdoll bone.
- Front/back get-up trigger support and fallback pose blending.

## Runtime states

### Active

The capsule motor owns locomotion. The hidden Animator produces the pose, the foot-planting pass keeps the stance near sampled ground, and the visible physical skeleton follows that final pose exactly. This is the stable PC baseline and does not use active-ragdoll springs while standing.

### Staggered

The character temporarily moves at reduced authority without entering a full ragdoll.

### Ragdoll

A sufficiently hard impact disables the capsule collision and switches every generated bone Rigidbody to dynamic simulation. The motor's latest linear velocity, angular velocity, and per-bone point velocity are transferred into the ragdoll, so a moving character continues travelling and tumbling naturally.

Generated ragdoll self-collisions are disabled by default to prevent automatically fitted colliders from exploding apart.

### Getting Up

The motor is repositioned beneath the fallen hips. The controller triggers `GetUpFront` or `GetUpBack` when those Animator parameters exist, then blends the visible body back to the animated target before re-enabling movement. Navigation is rebound to the motor and the current destination is repathed after activation.

## Grounded procedural stepping

When the target Animator has no Runtime Animator Controller, `ApexPCProceduralGait` supplies arm and leg movement. `ApexPCFootPlanting` then performs a grounded correction pass:

1. Each foot stores a planted world-space position.
2. The body moves past the planted foot.
3. When the stance error is large enough, one foot begins a step.
4. The foot travels along a lifted arc to a new sampled ground point.
5. A lightweight two-bone solve rotates the upper and lower leg toward the planted target.

The feet are part of the stable animated target while Active. They become fully dynamic with the rest of the body during Ragdoll.

## Motor-driven navigation

The legacy `NavMeshAgent`, navigator, and behavior brain may remain attached to the generated physical hips for serialization compatibility. `ApexPCNavigationDriver` projects path planning beneath the authoritative motor and synchronizes the agent's internal position to that projected point.

The driver also:

- Wakes a sleeping motor while a destination is active.
- Retries invalid and partial paths.
- Detects a destination that is requesting movement without making progress.
- Rebinds and repaths after get-up recovery.

The PC character inspector displays **On NavMesh**, **Has Destination**, **Has Complete Path**, **Path Status**, and **Remaining Distance** for troubleshooting.

## Optional Animator parameters

- `Speed` — Float
- `Moving` — Bool
- `Grounded` — Bool
- `Ragdoll` — Bool
- `GetUpFront` — Trigger
- `GetUpBack` — Trigger

The character still recovers without authored get-up clips, but an Animator Controller is required for a real get-up animation. Without an Animator Controller, Apex uses its procedural walking and planted-foot fallback while the NPC moves.

## Upgrade an older generated NPC

Select the generated humanoid root or one of its children, then choose:

**Apex Physics Engine > Characters > Rebuild Selected PC Physical Character**

The repair command removes the experimental locoball, foot-tether, torso-harness, and old physical-controller objects before installing the unified PC stack:

- Embedded motor fitting
- Procedural gait
- Grounded foot planting
- Motor-driven navigation
- Momentum-preserving ragdoll handoff

## Current limitation

The Active state uses exact animated pose following for stability. The planted-step solver makes the walking pose grounded and coherent, but the standing legs are not yet force-driven active-ragdoll muscles. Fully force-reactive standing locomotion will be reintroduced only after navigation, impacts, ragdoll, and recovery work reliably across multiple Humanoid models. VR support comes after this PC milestone.

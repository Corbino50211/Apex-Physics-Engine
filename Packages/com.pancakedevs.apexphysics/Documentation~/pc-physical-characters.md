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
- Impact-triggered ragdoll release.
- Front/back get-up trigger support and fallback pose blending.
- A procedural Humanoid gait when no Animator Controller is assigned.

## Runtime states

### Active

The capsule motor owns locomotion. The hidden Animator produces the pose, and the visible physical skeleton follows that pose exactly. This is the stable PC baseline and does not use active-ragdoll springs while standing.

### Staggered

The character temporarily moves at reduced authority without entering a full ragdoll.

### Ragdoll

A sufficiently hard impact disables the capsule collision and switches every generated bone Rigidbody to dynamic simulation. Generated ragdoll self-collisions are disabled by default to prevent automatically fitted colliders from exploding apart.

### Getting Up

The motor is repositioned beneath the fallen hips. The controller triggers `GetUpFront` or `GetUpBack` when those Animator parameters exist, then blends the visible body back to the animated target before re-enabling movement.

## Optional Animator parameters

- `Speed` — Float
- `Moving` — Bool
- `Grounded` — Bool
- `Ragdoll` — Bool
- `GetUpFront` — Trigger
- `GetUpBack` — Trigger

The character still recovers without authored get-up clips, but an Animator Controller is required for a real get-up animation. Without an Animator Controller, Apex uses its procedural walking fallback while the NPC moves.

## Upgrade an older generated NPC

Select the generated humanoid root or one of its children, then choose:

**Apex Physics Engine > Characters > Rebuild Selected PC Physical Character**

The repair command removes the experimental locoball, foot-tether, torso-harness, and old physical-controller objects before installing the unified PC component.

## Current limitation

The Active state uses exact animated pose following for stability. Fully force-reactive standing muscles will be reintroduced only after locomotion, impacts, ragdoll, and recovery work reliably across multiple Humanoid models. VR support comes after this PC milestone.

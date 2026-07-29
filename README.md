# Apex Physics Engine

**A PC-first Unity physics framework for physical characters, props, and gameplay systems.**
By [PancakeDevs](https://github.com/Corbino50211).

> Make Everything Physical.

![Unity 6000.0+](https://img.shields.io/badge/Unity-6000.0%2B-black)
![Version 0.3.10](https://img.shields.io/badge/version-0.3.10-blue)
![Status: Active Development](https://img.shields.io/badge/status-active%20development-orange)

Apex turns animated humanoids into fully simulated characters that walk with real forces, plant their feet on real ground, fall into momentum-preserving ragdolls, and get back up on their own. Everything ships as modular components you drop onto existing prefabs — no scene manager, no framework lock-in, no render pipeline or networking dependency.

---

## Status

Apex is in active development at `0.3.x`. The API is usable but not frozen; minor versions may introduce breaking changes until `1.0`.

| Module | What it does |
| --- | --- |
| **Core** | `ApexBody` Rigidbody wrapper, reusable `ApexPhysicsProfile` assets, universal impact events |
| **Characters** | Physical humanoids, procedural gait, toe-bone-aware foot planting, torso harness, support rig |
| **Ragdoll** | Active ragdolls, momentum transfer on knockdown, automatic get-up recovery |
| **Navigation** | NPC brain with Idle/Wander/Chase modes, motor-driven NavMesh movement, auto-baking |
| **Interaction** | Force-driven grabbers, grabbables, authored grab points and poses |
| **Warehouse** | Crates, pallets, spawners, barcode-driven spawnable assets with editor previews |
| **Editor** | Humanoid converter, lighting bake workflows, setup menus, in-editor package updates |

VR character support is planned once the desktop character foundation is stable.

---

## Install

### Unity Package Manager (git URL)

**Window → Package Manager → + → Add package from git URL:**

```text
https://github.com/Corbino50211/Apex-Physics-Engine.git?path=/Packages/com.pancakedevs.apexphysics
```

### Local development

Clone the repository and open it directly as a Unity project. The package is embedded at `Packages/com.pancakedevs.apexphysics`, so edits apply immediately without reimporting.

### Requirements

- Unity **6000.0** or newer
- `com.unity.ai.navigation` **2.0.9** (resolved automatically)

### Updating

Apex checks for updates in-editor. Open **Apex Physics Engine → Updates** to check manually, or enable automatic checks in the updater window.

---

## Quick start

All setup tools live under the top-level **Apex Physics Engine** menu.

### Make an object physical

1. Select a GameObject with a Collider.
2. **Apex Physics Engine → Bodies → Make Selected Object an Apex Body**
3. **Apex Physics Engine → Profiles → Create Physics Profile**
4. Assign the profile to the `ApexBody` component and press **Apply Physics Profile**.

The object now has profile-driven Rigidbody settings, safe force helpers, velocity limiting, teleportation that clears stale motion, and impact events.

### Build a physical NPC

1. Select an imported Humanoid rig in the scene.
2. **Apex Physics Engine → Characters → Build Selected Humanoid as PC Physical NPC**
3. Bake a NavMesh, or use **Apex Physics Engine → Navigation → Create Navigation Surface**.
4. Press Play.

The character walks under motor forces, plants its feet against the ground, ragdolls on hard impacts, and stands back up.

> The source avatar must map **Left Toes** and **Right Toes** for automatic foot-axis detection. Rebuild existing NPCs once after updating Apex.

---

## Usage

### Applying forces

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class PushApexBody : MonoBehaviour
{
    [SerializeField] private ApexBody target;
    [SerializeField] private Vector3 impulse = new Vector3(0f, 2f, 5f);

    public void Push()
    {
        target.ApplyImpulse(impulse);
    }
}
```

### Reacting to impacts

`ApexImpactInfo` carries contact point, normal, relative velocity, and impulse — enough to drive damage, audio, particles, or haptics without coupling to the body itself.

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class ImpactAudio : MonoBehaviour
{
    [SerializeField] private ApexBody body;
    [SerializeField] private AudioSource source;
    [SerializeField] private float minimumImpulse = 2f;

    private void OnEnable() => body.Impacted += OnImpacted;
    private void OnDisable() => body.Impacted -= OnImpacted;

    private void OnImpacted(ApexImpactInfo impact)
    {
        if (impact.Impulse < minimumImpulse)
        {
            return;
        }

        source.volume = Mathf.Clamp01(impact.Impulse / 20f);
        source.Play();
    }
}
```

### Driving an NPC

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class ChasePlayer : MonoBehaviour
{
    [SerializeField] private ApexPCPhysicalCharacter character;
    [SerializeField] private Transform player;

    private void Start()
    {
        character.SetTarget(player);
    }

    public void Knock()
    {
        character.KnockDown();  // ragdolls, then recovers automatically
    }
}
```

---

## Samples

Import **Physics Lab** from the Package Manager window for a scene wired up with Apex bodies, grabbables, and a physical character to test against.

---

## Documentation

Full guides live in [`Packages/com.pancakedevs.apexphysics/Documentation~/`](Packages/com.pancakedevs.apexphysics/Documentation~/):

| Guide | Topic |
| --- | --- |
| [getting-started.md](Packages/com.pancakedevs.apexphysics/Documentation~/getting-started.md) | Install and first Apex body |
| [characters.md](Packages/com.pancakedevs.apexphysics/Documentation~/characters.md) | Physical character overview |
| [pc-physical-characters.md](Packages/com.pancakedevs.apexphysics/Documentation~/pc-physical-characters.md) | Desktop character rig in depth |
| [animated-physical-npcs.md](Packages/com.pancakedevs.apexphysics/Documentation~/animated-physical-npcs.md) | Animated target driving |
| [humanoid-converter.md](Packages/com.pancakedevs.apexphysics/Documentation~/humanoid-converter.md) | Converting imported rigs |
| [supported-humanoids.md](Packages/com.pancakedevs.apexphysics/Documentation~/supported-humanoids.md) | Rig requirements |
| [navigation.md](Packages/com.pancakedevs.apexphysics/Documentation~/navigation.md) | NavMesh, brains, motors |
| [grabbing.md](Packages/com.pancakedevs.apexphysics/Documentation~/grabbing.md) | Grabbers and grab points |
| [warehouse.md](Packages/com.pancakedevs.apexphysics/Documentation~/warehouse.md) | Crates, pallets, spawners |
| [lighting.md](Packages/com.pancakedevs.apexphysics/Documentation~/lighting.md) | Bake presets and workflows |
| [updates.md](Packages/com.pancakedevs.apexphysics/Documentation~/updates.md) | In-editor package updater |

Version history is tracked in [CHANGELOG.md](Packages/com.pancakedevs.apexphysics/CHANGELOG.md).

---

## Architecture

**Namespace:** `PancakeDevs.ApexPhysics`
**Assemblies:** `PancakeDevs.ApexPhysics.Runtime`, `PancakeDevs.ApexPhysics.Editor`

Design rules the codebase holds itself to:

- Runtime code must not depend on a render pipeline.
- Core code must not depend on a networking library.
- Editor-only code stays outside runtime assemblies.
- Features are modular components, never one large manager.
- Existing Unity project settings are never changed silently.

---

## Contributing

This is a private-development project and external pull requests aren't being accepted yet. Bug reports via [Issues](https://github.com/Corbino50211/Apex-Physics-Engine/issues) are welcome.

Repository conventions:

- Every asset needs a committed `.meta` file — CI runs `Tools/validate_unity_meta.py` on push and PR.
- Version bumps update `package.json` and `CHANGELOG.md` in the same change.

---

## License

Copyright © PancakeDevs. All rights reserved while the project is under private development.

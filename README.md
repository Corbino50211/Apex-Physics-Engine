# Apex Physics Engine

**A PC-first Unity physics framework for physical characters, props, destruction, water, and gameplay systems.**
By [PancakeDevs](https://github.com/Corbino50211).

> Make Everything Physical.

![Unity 6000.0+](https://img.shields.io/badge/Unity-6000.0%2B-black)
![Version 0.5.0](https://img.shields.io/badge/version-0.5.0-blue)
![Status: Active Development](https://img.shields.io/badge/status-active%20development-orange)

Apex provides grounded PC physical characters with motor-driven locomotion, procedural foot planting, momentum-preserving ragdolls, automatic get-up recovery, force-driven interaction, editor-generated destructible meshes, synchronized water volumes, buoyancy, currents, and input-agnostic swimming. Everything is modular — no required scene manager, networking library, or render pipeline.

---

## Status

Apex is in active development at `0.5.x`. The API is usable but not frozen; minor versions may introduce breaking changes until `1.0`.

| Module | What it does |
| --- | --- |
| **Core** | `ApexBody` Rigidbody wrapper, reusable `ApexPhysicsProfile` assets, universal impact events |
| **Characters** | PC physical humanoids, embedded character motor, natural procedural gait, planted feet, single-owner foot orientation |
| **Ragdoll** | Active ragdolls, momentum transfer on knockdown, automatic get-up recovery |
| **Destruction** | Editor-generated mesh fracture, interior polygons, manual or force-threshold runtime activation, momentum-preserving debris |
| **Water** | Animated water volumes, synchronized GPU/CPU waves, currents, multi-point buoyancy, swimming, oxygen, underwater queries |
| **Navigation** | NPC brain with Idle/Wander/Chase modes, motor-driven NavMesh movement, auto-baking |
| **Interaction** | Force-driven grabbers, grabbables, authored grab points and poses |
| **Warehouse** | Crates, pallets, spawners, barcode-driven spawnable assets with editor previews |
| **Editor** | Humanoid conversion, destruction and water authoring, lighting workflows, setup menus, in-editor package updates |

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

The character moves through an embedded Rigidbody motor, plants its feet against the ground, ragdolls on hard impacts, and stands back up automatically.

> Apex 0.3.12 and newer do not require Humanoid toe-bone mappings for final foot orientation. The system preserves each avatar's original foot rotation, aligns it to the floor, and applies one controlled toe-up pitch.

### Make a mesh destructible

1. Place a closed mesh prefab into a scene.
2. Select the GameObject containing its `MeshFilter` and `MeshRenderer`.
3. **Apex Physics Engine → Destruction → Fracture Selected Mesh...**
4. Choose a target chunk count and optional interior material.
5. Click **Generate / Rebuild Fracture**.
6. On `ApexDestructible`, choose **Manual Only** or **Impact Threshold**.
7. For automatic breakage, choose **Collision Impulse** or **Estimated Force** and set **Break Threshold**.
8. Press Play and strike the object hard enough, or trigger it manually.

Apex slices the source mesh in the editor, generates new polygons across the exposed interior surfaces, creates centered debris chunks with convex colliders, and transfers the intact object's motion into released pieces. The generated geometry is editor-time; runtime activation can still happen automatically from force.

> The 0.4.x foundation expects a closed, manifold mesh with real volume. Open planes, paper-thin geometry, and heavily self-intersecting meshes may not fracture correctly.

### Create water and buoyancy

1. **Apex Physics Engine → Water → Create Water Volume**.
2. Select a Rigidbody prop and choose **Make Selected Rigidbody Buoyant**.
3. On `ApexBuoyantBody`, click **Generate Points From Colliders**.
4. Press Play and drop the object into the water.

Apex generates a saved low-poly surface, keeps CPU physics waves synchronized with the shader, applies currents and per-point buoyancy, and supports overlapping water volumes without duplicate multi-collider enter/exit events.

For swimming, add `ApexSwimmer` to a Rigidbody and feed `SetMoveInput`, `SetVerticalInput`, and `SetSprint` from the Input System, AI, networking, or future VR controls. Import **Apex Water URP Visuals** from Package Manager for the optional depth-color, foam, caustics, and refraction shader.

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

`ApexImpactInfo` carries contact point, normal, relative velocity, and impulse — enough to drive damage, audio, particles, haptics, or destruction without coupling to the body itself.

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

### Breaking a destructible from code

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class BreakTarget : MonoBehaviour
{
    [SerializeField] private ApexDestructible target;

    public void Explode(Vector3 worldPoint, float impulse)
    {
        target.BreakAt(worldPoint, Vector3.up, impulse);
    }
}
```

### Querying and controlling water

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class WaterController : MonoBehaviour
{
    [SerializeField] private ApexSwimmer swimmer;

    private void Update()
    {
        Vector3 point = transform.position;
        float surfaceHeight = ApexWaterManager.GetWaterHeight(point);
        bool underwater = ApexWaterManager.IsUnderwater(point);

        swimmer.SetInput(new Vector2(0f, 1f), underwater ? 0f : -1f, false);
    }
}
```

---

## Samples

Import **Physics Lab** from Package Manager for Apex bodies, grabbables, and a physical character.

Import **Apex Water URP Visuals** for the optional advanced URP shader. Apex does not automatically change URP Depth Texture, Opaque Texture, or other project settings.

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
| [destruction.md](Packages/com.pancakedevs.apexphysics/Documentation~/destruction.md) | Mesh fracture and runtime destruction |
| [water.md](Packages/com.pancakedevs.apexphysics/Documentation~/water.md) | Water volumes, buoyancy, currents, and swimming |
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
- Editor-only geometry generation stays outside runtime assemblies.
- Render-pipeline-specific visuals are optional samples rather than hard dependencies.
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

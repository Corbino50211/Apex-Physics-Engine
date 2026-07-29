# Apex Physics Engine

A PC-first modular Unity physics framework from PancakeDevs for physical characters, props, interaction, navigation, warehouse content, destructible meshes, and water physics.

## Current package version

`0.5.0`

## Included systems

- `ApexBody` and reusable physics profiles
- PC physical humanoids with procedural gait, grounded feet, ragdoll, and recovery
- force-driven grabbing and authored grab points
- automatic NavMesh baking and Idle/Wander/Chase NPC behavior
- barcode-driven crates, pallets, spawners, and Void previews
- editor-generated destructible mesh chunks with manual or impact-threshold activation
- synchronized water volumes, GPU/CPU waves, currents, Rigidbody buoyancy, and swimming
- optional oxygen and underwater camera fog
- automatic lighting workflows and in-editor package updates

## Water quick start

1. Choose **Apex Physics Engine > Water > Create Water Volume**.
2. Select any Rigidbody prop and choose **Make Selected Rigidbody Buoyant**.
3. Use **Generate Points From Colliders** for stable multi-point buoyancy.
4. Add `ApexSwimmer` to a Rigidbody character and feed its input API from the Input System, AI, networking, or VR.
5. Optionally import **Apex Water URP Visuals** from Package Manager for depth color, foam, caustics, and refraction.

Generated water meshes and materials are stored under `Assets/Apex Generated/Water/`. Apex never changes URP Depth Texture, Opaque Texture, or other project settings automatically. See `Documentation~/water.md` for the full API and limitations.

## Destruction quick start

1. Put a closed `MeshFilter` object into a scene.
2. Select the object.
3. Choose **Apex Physics Engine > Destruction > Fracture Selected Mesh...**.
4. Choose the chunk count and optional interior material.
5. Click **Generate / Rebuild Fracture**.
6. On `ApexDestructible`, choose **Manual Only** or **Impact Threshold**.
7. For automatic breakage, choose **Collision Impulse** or **Estimated Force** and set **Break Threshold**.

Generated destruction assets are stored under `Assets/Apex Generated/Destruction/`. Mesh slicing remains editor-time, while the pre-generated chunks can activate automatically from force during Play Mode.

Use the **Apex Physics Engine** menu in Unity's top bar for all setup tools.

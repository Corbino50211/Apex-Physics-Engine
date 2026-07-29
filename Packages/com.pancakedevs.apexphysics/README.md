# Apex Physics Engine

A PC-first modular Unity physics framework from PancakeDevs for physical characters, props, interaction, navigation, warehouse content, and destructible meshes.

## Current package version

`0.4.0`

## Included systems

- `ApexBody` and reusable physics profiles
- PC physical humanoids with procedural gait, grounded feet, ragdoll, and recovery
- force-driven grabbing and authored grab points
- automatic NavMesh baking and Idle/Wander/Chase NPC behavior
- barcode-driven crates, pallets, spawners, and Void previews
- editor-generated destructible mesh chunks with interior cap polygons
- partial impact breakage, secondary chunk release, and momentum-preserving debris
- automatic lighting workflows and in-editor package updates

## Destruction quick start

1. Put a closed `MeshFilter` object into a scene.
2. Select the object.
3. Choose **Apex Physics Engine > Destruction > Fracture Selected Mesh...**.
4. Choose the chunk count and optional interior material.
5. Click **Generate / Rebuild Fracture**.

Generated mesh assets are stored under `Assets/Apex Generated/Destruction/`. See `Documentation~/destruction.md` for supported geometry and current limitations.

Use the **Apex Physics Engine** menu in Unity's top bar for all setup tools.

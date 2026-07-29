# Apex Destruction

Apex 0.4.0 introduces editor-generated mesh fracture and impact-driven runtime destruction.

## Supported source objects

The first destruction foundation supports a scene object with:

- One `MeshFilter` and `MeshRenderer` on the selected GameObject.
- A closed, manifold mesh with real volume.
- Readable mesh data. Enable **Read/Write** in the model importer if Unity blocks mesh access.

Open surfaces, single planes, paper-thin geometry, non-manifold meshes, and heavily self-intersecting meshes may not produce valid capped chunks.

## Generate a fracture

1. Put the mesh prefab into a scene.
2. Select the GameObject containing its `MeshFilter` and `MeshRenderer`.
3. Choose **Apex Physics Engine > Destruction > Fracture Selected Mesh...**.
4. Choose a target chunk count and optional interior material.
5. Click **Generate / Rebuild Fracture**.
6. Save the scene or prefab instance.

Apex creates generated mesh assets under:

```text
Assets/Apex Generated/Destruction/
```

It also adds the required intact collider, kinematic Rigidbody, `ApexBody`, and `ApexDestructible` when they are missing.

## Fracture settings

- **Target Chunk Count** controls the requested number of pieces. Difficult geometry may produce fewer valid chunks.
- **Random Seed** makes a fracture repeatable.
- **Minimum Chunk Volume** rejects tiny pieces.
- **Fracture Irregularity** tilts cuts away from a clean grid.
- **Interior UV Scale** controls planar UV density on newly generated cut surfaces.
- **Interior Material** is assigned to the generated cap polygons. When empty, Apex reuses the final source material.

## Runtime behavior

The object remains intact until an impact exceeds **Break Impulse**.

On the first break:

1. The intact renderers and colliders are disabled.
2. All generated chunks become visible.
3. Chunks inside **Impact Radius** become dynamic Rigidbodies.
4. Remaining chunks stay visible and kinematic.
5. Later impacts can release additional nearby chunks.

Released chunks inherit the intact Rigidbody's linear and angular velocity. Apex also applies an outward impulse from the impact point.

## Inspector testing

During Play Mode, select the `ApexDestructible` and use:

- **Break At Center** to test a local partial break.
- **Break All** to release every chunk.

## Cleanup

Use **Clear Generated Fracture** in the `ApexDestructible` inspector or fracture window. Apex removes both the generated hierarchy and the generated mesh asset folder.

## Current limitations

- Fracturing happens in the Unity Editor, not during gameplay.
- The initial release supports one `MeshFilter` per destructible object.
- Generated chunks use convex `MeshCollider` components.
- Complex meshes can exceed Unity convex-collider cooking limits.
- Network replication and save-state restoration are not included yet.
- Runtime slicing and bullet-hole remeshing are later milestones.

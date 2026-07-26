# Apex Automatic Lighting Baker

Apex Physics Engine 0.0.6 adds an Editor-only lighting workflow for Unity 6.

## Open the lighting tools

Choose:

```text
Apex Physics Engine > Lighting > Open Lighting Baker
```

The window shows the active scene, whether lighting data exists, whether Apex considers the scene dirty, the last successful Apex bake, and live bake progress.

## Bake before Play Mode

With **Bake Before Play** enabled, pressing Unity's Play button follows this flow:

1. Apex checks whether lighting data is missing or the scene's lighting hash changed.
2. If the scene is current and **Only When Dirty** is enabled, Play Mode begins normally.
3. If a bake is required, Apex cancels the first Play request.
4. Apex saves modified scenes after confirmation, applies the selected quality preset, and starts an asynchronous lightmap bake.
5. Enabled Baked-mode reflection probes are optionally baked after the lightmap job.
6. Apex stores the new scene hash and automatically enters Play Mode.

Disable **Enter Play After Bake** when you want the bake to finish without starting the game.

## Dirty detection

Apex includes these inputs in the active scene hash:

- Light transforms and baked-light settings
- GI-contributing MeshRenderers, meshes, materials, and transforms
- GI-contributing Terrains and TerrainData
- Reflection Probe transforms and bake settings
- Render Settings, skybox, and sun assignment
- Lighting Settings and sample values

Use **Mark Lighting Dirty** when a custom tool changes lighting-relevant data that Apex does not inspect.

## Quality presets

### Preview

Designed for quick iteration:

- 10 lightmap texels per world unit
- 16 direct samples
- 64 indirect samples
- 32 environment samples
- 1 bounce

### Production

Designed for a higher-quality final bake:

- 40 lightmap texels per world unit
- 64 direct samples
- 512 indirect samples
- 256 environment samples
- 4 bounces

### Custom

Exposes lightmap resolution, direct samples, indirect samples, environment samples, and maximum bounces in the Apex window.

Changing a preset marks the active scene dirty. Click **Apply Selected Preset to Scene** to apply it without immediately baking.

## Reflection probes

Apex bakes enabled Reflection Probes whose mode is **Baked**. Generated cubemaps are stored beside the scene in:

```text
<SceneName>_ApexLighting/
```

Reflection-probe baking is synchronous, so Unity may pause briefly for each probe after the asynchronous lightmap job completes.

## Commands

```text
Apex Physics Engine
└── Lighting
    ├── Open Lighting Baker
    ├── Bake and Enter Play Mode
    ├── Bake Lighting Now
    ├── Bake Reflection Probes
    ├── Apply Selected Preset
    ├── Mark Lighting Dirty
    ├── Clear Lighting Data
    └── Cancel Active Bake
```

## Important limitation

This is an Editor workflow. Apex does not calculate baked lightmaps in a standalone player. Builds use lighting data generated and saved by the Unity Editor.

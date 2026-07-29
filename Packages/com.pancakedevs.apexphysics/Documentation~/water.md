# Apex Water

Apex Water integrates water volumes, synchronized wave queries, Rigidbody buoyancy, currents, swimming, oxygen, and optional underwater camera fog into Apex Physics Engine.

## Architecture

The module is split deliberately:

- **Runtime water physics** is render-pipeline independent.
- **Apex Simple Water** is included and works as a lightweight transparent fallback.
- **Apex Water URP Visuals** is an optional Package Manager sample with depth color, foam, caustics, and refraction.
- Apex never changes renderer assets or project graphics settings automatically.

## Create a water volume

Choose:

```text
Apex Physics Engine
→ Water
→ Create Water Volume
```

Or add `ApexWaterVolume` to an upright GameObject with a `MeshFilter` and `MeshRenderer`.

The inspector can:

- Capture the logical volume bounds from the current mesh before surface generation.
- Generate and save a low-poly surface mesh under `Assets/Apex Generated/Water/`.
- Create a default water material.
- Apply wave and rendering properties using a `MaterialPropertyBlock`.
- Clear generated assets without removing the logical water component.

After the thin generated surface is assigned, logical-bound recapture is disabled to prevent the authored water depth from collapsing to the surface mesh. Set the volume fields directly or reassign the original source mesh before recapturing.

Water volumes should remain upright. Y-axis rotation is supported; X/Z tilt can make the horizontal physics surface disagree with the rendered mesh.

## Physics and visual synchronization

`ApexWaterMath` creates four fanned sine-wave layers. `ApexWaterVolume.GetWaterHeight` uses the same layer formula mirrored in both included shaders, allowing buoyancy and swimming to follow the animated visual surface.

```csharp
float height = ApexWaterManager.GetWaterHeight(worldPosition);
bool underwater = ApexWaterManager.IsUnderwater(worldPosition);
ApexWaterVolume volume = ApexWaterManager.FindVolume(worldPosition);
Vector3 current = ApexWaterManager.GetCurrent(worldPosition);
```

When water volumes overlap, Apex chooses the highest valid surface above the queried volume floor.

## Buoyant bodies

Select a Rigidbody and choose:

```text
Apex Physics Engine
→ Water
→ Make Selected Rigidbody Buoyant
```

`ApexBuoyantBody` supports multiple sample points for stable boats and props. Use **Generate Points From Colliders** to create corner and center points from the current collider bounds.

It applies:

- Point-based upward acceleration from submersion depth.
- Per-point vertical damping.
- Linear and angular water damping.
- Water currents.
- Different water surfaces per point when a large object crosses overlapping volumes.

It works with ordinary Rigidbodies, `ApexBody`, crates, generated destruction chunks, and individual ragdoll bodies.

## Swimming

`ApexSwimmer` intentionally does not own a specific input system. Feed it from the Unity Input System, AI, networking, or later VR controls:

```csharp
swimmer.SetMoveInput(moveAction.ReadValue<Vector2>());
swimmer.SetVerticalInput(verticalAction.ReadValue<float>());
swimmer.SetSprint(sprintAction.IsPressed());
```

Or use:

```csharp
swimmer.SetInput(movement, vertical, sprint);
```

The **Create Example Setup** command adds an input-agnostic swimmer and does not add a legacy input component. Connect your own Input System actions before expecting that example character to move.

The optional `ApexLegacySwimInput` component is only for quick testing when the Legacy Input Manager is enabled.

Swimming includes:

- Camera- or transform-relative movement.
- Ascending and descending.
- Sprinting.
- Passive surface settling.
- Current velocity.
- Floor protection.
- Optional oxygen and out-of-oxygen events.

## Underwater camera fog

`ApexUnderwaterCameraEffects` queries the camera position and toggles global `RenderSettings` fog. Because Unity fog is global, use one instance for the local player camera. A render-pipeline-specific volume/post-processing implementation can replace it later.

## Optional URP visuals

In Package Manager, import the **Apex Water URP Visuals** sample. Then regenerate the surface or assign a material using:

```text
Apex Physics Engine/Water/URP Low Poly
```

For depth color, shoreline foam, and refraction, manually enable **Depth Texture** and **Opaque Texture** on the active URP renderer. Apex does not modify those settings.

## Trigger behavior

`ApexWaterVolume` reference-counts colliders per `IApexWaterReactive` component. Multi-collider characters and ragdolls receive one enter callback when the first collider enters and one exit callback after the last collider leaves.

## Current limits

- Water surfaces are horizontal rather than arbitrary curved volumes.
- The optional advanced shader currently targets URP.
- The fallback shader does not perform depth-based shoreline foam or refraction.
- Buoyancy uses point sampling rather than full displaced-volume integration.
- Swimming is a Rigidbody motor, not yet a dedicated Apex PC-character swimming state.

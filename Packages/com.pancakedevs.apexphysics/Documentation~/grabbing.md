# Apex Grabbing

Apex grabbing is input-agnostic. VR controllers, desktop input, NPC logic, editor tests, and networking adapters all call the same public methods on `ApexGrabber`.

## Make an object grabbable

1. Select a scene object.
2. Choose **GameObject > Apex Physics > Make Selected Object Grabbable**.
3. Apex adds a Collider when missing, plus `Rigidbody`, `ApexBody`, and `ApexGrabbable`.
4. Optionally choose **GameObject > Apex Physics > Add Grab Point** to create an authored handle.

## Create a grabber

1. Create or select a GameObject representing a hand or grip target.
2. Choose **GameObject > Apex Physics > Make Selected Object a Grabber**.
3. Create a profile with **Assets > Create > PancakeDevs > Apex Physics > Grab Profile**.
4. Assign that profile to `ApexGrabber`.

## Test without an input package

1. Enter Play Mode.
2. Select the GameObject containing `ApexGrabber`.
3. Place its grab-radius gizmo near a grabbable object.
4. Click **Grab Closest** in the custom inspector.
5. Move or rotate the grabber GameObject while the game is running.
6. Click **Release / Throw**.

The object is driven with forces and torque. It is not parented or teleported to the hand.

## Input integration

Call these public methods from an input action, XR controller event, NPC behavior, or custom script:

```csharp
apexGrabber.TryGrabClosest();
apexGrabber.Release();
```

Call `TryGrabClosest` when the grip begins and `Release` when it ends.

## Profiles

`ApexGrabProfile` controls:

- positional spring and damping
- maximum pulling force
- rotational spring and damping
- maximum torque
- maximum held mass
- grip break distance
- release velocity influence
- throw multiplier and maximum throw speed

Lower forces make heavy objects lag behind the target. Higher forces create a tighter arcade-style grip.
# Getting Started

## Install the embedded package

This repository develops Apex as an embedded Unity Package Manager package at:

```text
Packages/com.pancakedevs.apexphysics
```

Open the repository as a Unity project, or copy that package folder into another Unity project's `Packages` directory during local development.

## Create an Apex body

1. Select a GameObject with a Collider.
2. Choose **GameObject > Apex Physics > Make Selected Object an Apex Body**.
3. Create a profile with **Assets > Create > PancakeDevs > Apex Physics > Physics Profile**.
4. Assign the profile to the `ApexBody` component.
5. Press **Apply Physics Profile** in the inspector.

The object now supports consistent profile settings, force helpers, safe teleporting, velocity limiting, sleeping controls, and impact events.

## Runtime example

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

## Impact example

```csharp
using PancakeDevs.ApexPhysics;
using UnityEngine;

public sealed class ImpactLogger : MonoBehaviour
{
    [SerializeField] private ApexBody body;

    private void OnEnable()
    {
        body.Impacted += OnImpacted;
    }

    private void OnDisable()
    {
        body.Impacted -= OnImpacted;
    }

    private static void OnImpacted(ApexImpactInfo impact)
    {
        Debug.Log($"Impact speed: {impact.Speed:0.00}, impulse: {impact.Impulse:0.00}");
    }
}
```

## Current limitations

Version `0.0.1` is the foundation only. Physical hands, grabbing, players, active-ragdoll NPCs, damage, and mechanisms will be developed on top of this core.

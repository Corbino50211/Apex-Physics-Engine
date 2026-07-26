# Apex Warehouse and Crates

Apex Physics Engine 0.0.9 introduces a barcode-driven content system for reusable gameplay prefabs.

The goal is to let a level store lightweight spawn instructions while the actual prefab content lives in reusable crate assets.

## Core structure

```text
Apex Warehouse
└── Apex Pallet
    ├── Spawnable Crate
    ├── Spawnable Crate
    └── Spawnable Crate
```

- **ApexSpawnableCrate** references one prefab and owns a stable barcode.
- **ApexPallet** groups crates that should be registered together.
- **ApexWarehouse** registers pallets and resolves crates by barcode at runtime.
- **ApexCrateSpawner** is placed in a scene and creates a crate prefab after the level loads.
- **ApexCrateInstance** records which crate and spawner produced a runtime object.

## Create a spawnable crate

1. Create or import a prefab.
2. Select the prefab in the Project window.
3. Choose:

```text
Apex Physics Engine
→ Warehouse
→ Create Spawnable Crate From Selected Prefab
```

Apex creates the crate under:

```text
Assets/Apex Physics Engine/Warehouse/Crates/
```

The crate receives a stable 32-character barcode. Do not regenerate the barcode after levels, saves, or networking systems begin referencing it unless you intend to break those references.

## Create a pallet

Select one or more Apex crate assets, then choose:

```text
Apex Physics Engine
→ Warehouse
→ Create Pallet From Selected Crates
```

You can also create an empty pallet and populate its crate list manually.

## Create the runtime warehouse

Choose:

```text
Apex Physics Engine
→ Warehouse
→ Create Warehouse
```

Add the required Pallet assets to **Preloaded Pallets**. The warehouse registers those crates during `Awake` and can remain alive between scene loads.

A direct crate reference on a spawner also registers itself automatically, so a warehouse pallet is mainly needed for barcode-only references and centralized content loading.

## Place a crate spawner

Select a Spawnable Crate asset and choose:

```text
Apex Physics Engine
→ Warehouse
→ Create Crate Spawner
```

The spawner stores the crate and its barcode. Its default timing is **End Of Frame**, which gives scene objects and the warehouse time to finish loading before the prefab is created.

Available timings:

- **Awake** — spawn immediately when the marker awakens.
- **Start** — spawn during the marker's Start phase.
- **End Of Frame** — recommended level-load behavior.
- **Manual** — only spawn through code or the Play Mode inspector button.

A spawner can create multiple copies using **Spawn Count** and **Position Step**.

## Runtime API

Spawn directly from a crate:

```csharp
ApexWarehouse warehouse = ApexWarehouse.GetOrCreate();
GameObject instance = warehouse.Spawn(
    crate,
    position,
    rotation);
```

Spawn by barcode:

```csharp
if (warehouse.TrySpawn(barcode, position, rotation, out GameObject instance))
{
    // The crate resolved and spawned successfully.
}
```

Create a manual scene marker:

```csharp
spawner.SetCrate(crate);
spawner.SpawnAll();
```

## Validation

Run:

```text
Apex Physics Engine
→ Warehouse
→ Validate Warehouse Assets
```

Validation reports:

- Duplicate crate barcodes
- Spawnable crates with missing prefabs
- The total number of valid crate assets

## Current scope

Version 0.0.9 uses direct Unity prefab references. Future warehouse adapters can add Addressables, asset bundles, mod pallets, asynchronous loading, dependency tracking, and network spawn registration without changing the crate barcode API.

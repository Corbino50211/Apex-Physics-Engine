# Apex Physics Engine

**Apex Physics Engine** is a modular Unity physics framework by **PancakeDevs**.

Its goal is to make physical players, NPCs, grabbable objects, mechanisms, damage, and other physics-driven gameplay systems easy to install and reuse across Unity projects.

> Make Everything Physical.

## Current status

Apex is in early development. The first milestone is the core runtime package:

- `ApexBody` for consistent Rigidbody behavior
- reusable `ApexPhysicsProfile` assets
- collision and impact data
- editor validation and setup tools
- a Physics Lab sample scene

## Package location

The Unity Package Manager package is developed in:

```text
Packages/com.pancakedevs.apexphysics/
```

## Development rules

- Runtime code must not depend on a render pipeline.
- Core code must not depend on a networking library.
- Editor-only code stays outside runtime assemblies.
- Features should be modular components rather than one large manager.
- Existing Unity project settings must not be changed silently.

## Namespace

```csharp
PancakeDevs.ApexPhysics
```

## License

Copyright © PancakeDevs. All rights reserved while the project is under private development.

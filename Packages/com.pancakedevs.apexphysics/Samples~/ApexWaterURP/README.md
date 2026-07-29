# Apex Water URP Visuals

This optional sample adds the advanced URP water shader:

```text
Apex Physics Engine/Water/URP Low Poly
```

It provides GPU waves, depth-based shallow/deep color, shoreline/intersection foam, Fresnel, optional caustics, and opaque-texture refraction.

Before using depth color, foam, or refraction, enable **Depth Texture** and **Opaque Texture** on the active URP renderer asset. Apex intentionally does not change those project settings automatically.

After importing the sample, select an `ApexWaterVolume` and click **Generate / Rebuild Surface (URP Visuals)**, or create a material with the shader and assign it manually.

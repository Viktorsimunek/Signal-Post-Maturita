# Terrain AntiRepeat

Local copy of Unity URP 17.3 Terrain/Lit, with two offset texture samples blended by smooth procedural noise and larger brightness variation. No noise texture required. Normal and mask maps use the same sampling as diffuse; alpha is preserved for smoothness/density blending. Supports terrain holes, instancing, additional layer passes, forward/deferred rendering and a matching generated distant basemap.

Assign Assets/Materials/TerrainAntiRepeat.mat in Terrain > Settings > Material. Existing painted TerrainLayers supply textures automatically. Original materials and scenes are unchanged.

- Anti Tile Blend: 0 disables offset blending; default 0.7.
- Variation Size: size in texture repetitions, independently following each layer tile size; default 8.
- Macro Color Variation: brightness amplitude; default 0.16. Zero disables it.

More than four layers use the custom additive dependency. Custom basemap generation bakes the same variation. After changing settings, reassign the material or refresh the terrain basemap (TerrainData.SetBaseMapDirty) if the distant appearance is stale. A low-resolution basemap naturally loses fine texture detail.

Cost: two samples instead of one for each layer diffuse/normal/mask texture plus procedural noise. Translation blending reduces obvious repetition but does not fully remove repeating landmarks. Stock terrain PBR lighting is retained; ClassicPSX mesh shader is untouched.

Validation: source/dependency checks only. Unity MCP reported no connected editor, so shader compilation and visual validation must be performed in Unity 6000.3 / URP 17.3.

## Perlin color overlay

Perlin Color controls the patch color (alpha also multiplies visibility). Perlin Visibility sets opacity, with 0 disabling the overlay. Perlin Size sets the noise cell size as a fraction of terrain width/length: 0.06 is approximately 60 metres on a 1000-metre terrain; larger values make larger patches. Two octaves of gradient Perlin noise are blended over the mixed albedo before lighting, retaining terrain shadows. The overlay uses common terrain UVs independently of layer tile sizes. It is included in additive layers and distant basemap generation; refresh the basemap after editing if needed. Each terrain tile has its own UV domain.

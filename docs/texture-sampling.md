# Texture sampling

`TextureSampling { Filter, Wrap }` (in `Yaeger.Platform`) controls how a texture is sampled. Both runtimes use the same default so a scene looks the same natively and in the browser.

| Filter | Meaning |
| --- | --- |
| `Nearest` | Point sampling — crisp pixel art |
| `Linear` | Bilinear, no mipmaps (default) |
| `LinearMipmap` | Trilinear through a generated mip chain |

`Wrap` is `Clamp` (default) or `Repeat`. Presets: `TextureSampling.Default` (Linear + Clamp), `Pixelated` (Nearest + Clamp), `Tiled` (LinearMipmap + Repeat).

## Native

```csharp
renderer.Textures.SetSampling("Assets/hero.png", TextureSampling.Pixelated); // per texture
renderer.Textures.DefaultSampling = TextureSampling.Pixelated;               // all later loads
var textures = new TextureManager(window.Gl, TextureSampling.Tiled);         // 3D materials
```

## Browser

```csharp
surface.SetDefaultSampling(TextureSampling.Pixelated); // before textures load
surface.SetSampling("Assets/hero.png", TextureSampling.Pixelated);
```

Mipmaps are off by default because they average neighbouring atlas entries when a tilemap or sheet is minified (zoomed out), causing seams. 3D materials with tiling textures should opt in with `TextureSampling.Tiled`.

## Atlas guidance

- Extrude each tile's edge pixels by 1–2 px into the padding.
- With `Linear`, inset UVs by half a texel so bilinear taps never reach a neighbouring tile.
- Spacing-free sheets also bleed with `Nearest` at non-integer pixels-per-tile scales (e.g. a zooming camera). `UnifiedRenderSystem` therefore insets `Tilemap` tile and `SpriteSheet` frame UVs by half a texel automatically whenever the render surface reports the texture size (`IRenderSurface.GetTextureSize`; native `Renderer` and `BrowserRenderSurface` do). Call `GetFrameUv`/`GetTileUv(index, textureSize, texelInset)` for a custom inset. Particle flipbooks are not yet inset.
- Use `Nearest` for pixel art and keep tile positions on whole pixels.

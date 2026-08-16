# Render-to-Texture and Post-Processing

`PostProcessStack` renders the scene into an offscreen target instead of straight to the window,
then runs the result through a chain of full-screen effects — vignette/colour-grade, bloom, or a
custom `IPostProcessEffect` — before the final pass writes to the backbuffer. This is what makes
"render the scene, then operate on the result" possible: bloom, vignette, colour grading, screen
transitions, and similar effects that plain forward rendering can't express.

> **Scope.** Effect shaders are inline GLSL like the rest of the engine, not data-driven asset
> files. The stack runs in LDR by default (bloom's threshold operates on `[0, 1]` colour values
> already in the scene's output range); pass `hdr: true` to switch to floating-point HDR targets
> and add a `ToneMapEffect` — see [HDR and tone mapping](#hdr-and-tone-mapping) below. Auto-exposure
> and HDR *display* output (as opposed to tone-mapping down to an SDR backbuffer) are out of scope.

## How it works

Each frame, `PostProcessStack.Render(renderScene)`:

1. Binds an offscreen **scene target** and calls `renderScene` — your existing
   `UnifiedRenderSystem.Render()` or `MeshRenderSystem.Render()` call, unchanged. Whichever
   framebuffer is bound when a render system runs is where it draws; the render systems
   themselves need no post-processing-specific code.
2. Runs every enabled effect in `Effects` order, in a single pass each unless the effect itself
   is multi-pass (see bloom below). Consecutive effects **ping-pong** between two offscreen
   targets — effect *N*'s output becomes effect *N+1*'s input — so the chain needs only two extra
   targets regardless of its length.
3. The last enabled effect writes directly to the window's backbuffer. With zero enabled effects,
   the scene target is blitted straight through unchanged.

This ordering (which surface each pass reads/writes) is planned by `PostProcessPlanner.Plan`, a
pure C# function with no GL calls — see [Testing](#testing) below.

Render UI/inspector overlays **after** `PostProcessStack.Render` returns, so they land on the
backbuffer untouched by the effect chain:

```csharp
window.OnRender += delta =>
{
    postProcessStack.Render(meshRenderSystem.Render);
    inspector.Render(delta); // unaffected by vignette/bloom
};
```

## Usage

```csharp
using var postProcessStack = new PostProcessStack(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    sceneHasDepth: true); // true for a 3D scene (MeshRenderSystem depth-tests against it), false for 2D

using var vignette = new VignetteEffect(window.Gl) { Intensity = 0.5f };
using var bloom = new BloomEffect(window.Gl, (int)window.Size.X, (int)window.Size.Y) { Threshold = 0.8f };

postProcessStack.Effects.Add(vignette);
postProcessStack.Effects.Add(bloom);

// Keep offscreen targets sized to the window.
window.OnResize += size => postProcessStack.Resize((int)size.X, (int)size.Y);

window.OnRender += delta =>
{
    postProcessStack.Render(meshRenderSystem.Render);
    inspector.Render(delta);
};
```

Toggle an individual effect at runtime via its `Enabled` property, or bypass the whole stack via
`PostProcessStack.Enabled`:

```csharp
bloom.Enabled = false;              // scene → vignette → backbuffer
postProcessStack.Enabled = false;   // renderScene draws straight to the backbuffer, as if there
                                     // were no stack at all
```

This makes disabling the stack (or every effect in it) render exactly as it did before
post-processing existed — no extra offscreen copy, no different output.

## Built-in effects

### `VignetteEffect` — single pass

Darkens the frame towards the edges and applies a saturation/tint colour grade. One shader, one
draw — the simplest possible chain entry.

| Property | Meaning |
| --- | --- |
| `Intensity` | Edge darkening strength: 0 = none, 1 = fully black at the edges. |
| `Radius` | Distance from centre (UV units, 0.5 = frame edge) where darkening starts. |
| `Softness` | Width of the fade from full brightness to fully vignetted. |
| `Saturation` | 1 = unchanged, 0 = grayscale, >1 = boosted. |
| `Tint` | Multiplicative colour tint; `(1,1,1)` is neutral. |

### `BloomEffect` — multi-pass

Extracts pixels brighter than `Threshold`, blurs them with a separable Gaussian kernel
(`BlurIterations` horizontal+vertical pairs, ping-ponging between two **half-resolution** targets
it owns internally), then composites the blur back onto the original source additively. This is
the effect that exercises the multi-pass shape: threshold → blur ×N → composite, all inside one
chain entry.

| Property | Meaning |
| --- | --- |
| `Threshold` | Brightness (max colour channel) above which a pixel starts contributing. |
| `SoftKnee` | Width of the soft transition above `Threshold` before a pixel fully contributes. |
| `BlurIterations` | Number of horizontal+vertical blur pass pairs. Higher = softer glow, more cost. |
| `Intensity` | Strength the blurred bloom is added back at during composite. |

Its constructor also takes an optional `RenderTargetFormat` (default `Rgba8`) for its own internal
bright-pass/blur targets — pass `Rgba16F` when the stack is HDR, or match `Threshold` against
`RenderTargetFormat.Rgba8`'s implicit LDR at your peril (see [HDR and tone mapping](#hdr-and-tone-mapping)).
In an LDR chain, `Threshold`'s default of 1.0 (pure white) rarely triggers — this is unchanged
LDR behaviour. In an HDR chain, the same default becomes a meaningful cutoff: only pixels brighter
than diffuse white (e.g. `Material3D.EmissiveIntensity` above 1) contribute.

### `ToneMapEffect` — single pass, must run last

Compresses HDR colour (values that may exceed 1.0) down to the backbuffer's displayable `[0, 1]`
range, then gamma-encodes the result back to sRGB. Only meaningful in an HDR chain (see below), but
harmless to include in an LDR one — its input is already in `[0, 1]` there.

| Property | Meaning |
| --- | --- |
| `Operator` | `ToneMapOperator.Reinhard` or `ToneMapOperator.AcesFilmic` (default) — the compression curve. |
| `Exposure` | Multiplier applied to the HDR colour before tone mapping. 1 = no adjustment. |

## HDR and tone mapping

By default `PostProcessStack` allocates its scene and ping-pong targets as 8-bit `Rgba8` — every
colour value clamps to `[0, 1]` the moment it's written, so bloom's `Threshold` (which defaults to
1.0, pure white in that format) has nothing above it to grab, and an emissive material can never
read brighter than flat diffuse white.

Pass `hdr: true` to the `PostProcessStack` constructor to allocate those targets as floating-point
`Rgba16F` instead (`PostProcessPlanner.SelectSceneFormat` picks the format; see
[Testing](#testing)). Colour values above 1.0 — an emissive surface authored several times white via
`Material3D.EmissiveIntensity` (see docs/pbr.md), or a bright light accumulation — now survive
through the scene target and every effect in the chain instead of clamping away immediately.

```csharp
using var postProcessStack = new PostProcessStack(
    window.Gl, (int)window.Size.X, (int)window.Size.Y, sceneHasDepth: true, hdr: true);

using var bloom = new BloomEffect(
    window.Gl, (int)window.Size.X, (int)window.Size.Y, RenderTargetFormat.Rgba16F)
{
    Threshold = 1f, // now a meaningful cutoff: only pixels brighter than diffuse white bloom
};
using var toneMap = new ToneMapEffect(window.Gl); // must be the last effect added

postProcessStack.Effects.Add(bloom);
postProcessStack.Effects.Add(toneMap);
```

Three things to get right, all enforced or documented here:

1. **`BloomEffect`'s own internal buffers must match.** Bloom's bright-pass/blur targets are
   separate `RenderTarget`s it owns internally (see above) — pass the same `RenderTargetFormat` you
   gave the stack, or bloom's own buffers clamp away the over-1.0 values its threshold pass just
   extracted, before they ever reach the composite.
2. **`ToneMapEffect` must be the last enabled effect.** Every earlier effect in an HDR chain
   operates on linear, unclamped colour; `ToneMapEffect` is what compresses and gamma-encodes it.
   An effect placed after it would instead see already-compressed sRGB values. This is enforced at
   runtime — `IPostProcessEffect.RequiresLastPass` (true for `ToneMapEffect`, false by default) is
   checked by `PostProcessPlanner.ValidateOrdering`, which `PostProcessStack.Render` calls before
   planning passes; violating the ordering throws `InvalidOperationException`.
3. **`Renderer3D`'s own in-shader tone mapping must be turned off.** The PBR shading path
   Reinhard-tone-maps and gamma-encodes its result before writing it out, same as it always has —
   correct when rendering straight to an LDR target, wrong when rendering into an HDR scene target
   that a `ToneMapEffect` will compress afterwards (it would double-tone-map and double-gamma-encode).
   Construct `Renderer3D` with `hdrOutput: true` to skip that in-shader step and write linear HDR
   colour instead; see docs/pbr.md's colour-space section for exactly where each conversion happens.

With HDR disabled (the default, `hdr: false`/omitted, `Renderer3D`'s default `hdrOutput: false`),
every one of the above is a no-op and output is pixel-identical to the original LDR path — nothing
above uses format `Rgba16F`, `ToneMapEffect` is simply absent from the chain, and `Renderer3D` keeps
tone-mapping/gamma-encoding in-shader exactly as before this feature existed.

## Writing a custom effect

Implement `IPostProcessEffect`:

```csharp
public interface IPostProcessEffect : IDisposable
{
    bool Enabled { get; set; }
    void Apply(uint sourceColorTexture, uint destinationFramebuffer, int width, int height, FullscreenQuad quad);
    void Resize(int width, int height) { } // default no-op
}
```

`Apply` receives the previous pass's colour texture and must end by drawing into
`destinationFramebuffer` (0 means the backbuffer) at the given size, using `quad.Draw()` to submit
the full-screen triangle pair. A single-pass effect (like `VignetteEffect`) binds the destination
immediately; a multi-pass effect (like `BloomEffect`) may bind its own internal targets first for
intermediate passes, but must bind `destinationFramebuffer` again before its *final* draw —
`PostProcessStack` does not rebind it for you afterwards. Override `Resize` if your effect owns
extra offscreen targets (e.g. bloom's threshold/blur buffers); it's called whenever
`PostProcessStack.Resize` is.

## `RenderTarget`

The offscreen building block: an FBO wrapping a colour texture and an optional depth texture,
following `ShadowMapRenderer`'s pattern of a renderer owning its own GL handles.
`PostProcessStack` owns three — the scene target and two ping-pong targets — and resizes all three
(plus every effect's own targets) from `PostProcessStack.Resize`. `RenderTarget.Resize` recreates
the backing textures in place only when the size actually changed, and ignores a non-positive size
(e.g. a minimize event) so a still-valid target is never replaced with a degenerate one; the
`RenderTargetFormat` passed at construction (`Rgba8` or `Rgba16F`) is fixed for the target's
lifetime and preserved across every resize — a resize never leaks the old texture/FBO handles or
silently changes format.

A `RenderTarget` takes an optional `RenderTargetFormat format` (defaults to `Rgba8`, the original
LDR format); `PostProcessStack` picks it for its own three targets via
`PostProcessPlanner.SelectSceneFormat(hdr)`.

## Testing

`PostProcessPlanner.Plan` — which surface each pass reads from and writes to, given the indices of
the currently-enabled effects — is pure C# with no GL calls, and is unit-tested directly (see
`tests/Yaeger.Tests/Rendering/PostProcessPlannerTests.cs`), the same convention `MeshInstanceBatcher`
uses for its CPU-side grouping logic. `PostProcessPlanner.SelectSceneFormat` (HDR → format) and
`PostProcessPlanner.ValidateOrdering` (the `RequiresLastPass` check) are pure C# the same way, and
are unit-tested alongside `Plan` in the same file. Constructing a
`PostProcessStack`/`RenderTarget`/effect needs a live OpenGL context, so those aren't unit-tested —
see `docs/TESTING.md`.

## Sample

`Samples/PostProcessingDemo` renders a handful of emissive boxes on a dark floor through vignette +
bloom, in an HDR chain with `ToneMapEffect` last. Press **B** to toggle bloom, **V** to toggle
vignette, **T** to toggle tone mapping's operator (Reinhard/ACES filmic), **P** to toggle the whole
stack, and compare against the plain scene. One box is authored with `EmissiveIntensity` above 1 so
it blooms and reads as a genuine light source instead of clamping to flat white.

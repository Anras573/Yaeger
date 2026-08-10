# Render-to-Texture and Post-Processing

`PostProcessStack` renders the scene into an offscreen target instead of straight to the window,
then runs the result through a chain of full-screen effects — vignette/colour-grade, bloom, or a
custom `IPostProcessEffect` — before the final pass writes to the backbuffer. This is what makes
"render the scene, then operate on the result" possible: bloom, vignette, colour grading, screen
transitions, and similar effects that plain forward rendering can't express.

> **Scope (v1).** Effects run in LDR — no HDR pipeline or tone mapping yet (bloom's threshold
> operates on `[0, 1]` colour values already in the scene's output range). Effect shaders are
> inline GLSL like the rest of the engine, not data-driven asset files.

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
(e.g. a minimize event) so a still-valid target is never replaced with a degenerate one.

## Testing

`PostProcessPlanner.Plan` — which surface each pass reads from and writes to, given the indices of
the currently-enabled effects — is pure C# with no GL calls, and is unit-tested directly (see
`tests/Yaeger.Tests/Rendering/PostProcessPlannerTests.cs`), the same convention `MeshInstanceBatcher`
uses for its CPU-side grouping logic. Constructing a `PostProcessStack`/`RenderTarget`/effect needs
a live OpenGL context, so those aren't unit-tested — see `docs/TESTING.md`.

## Sample

`Samples/PostProcessingDemo` renders a handful of emissive boxes on a dark floor through vignette +
bloom. Press **B** to toggle bloom, **V** to toggle vignette, **P** to toggle the whole stack, and
compare against the plain scene.

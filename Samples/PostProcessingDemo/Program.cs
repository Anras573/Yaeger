using System.Numerics;
using PostProcessingDemo;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Post-processing demo (issues #151, #193): a dim scene with a handful of emissive "light" boxes,
// run through an HDR PostProcessStack chaining vignette/colour-grade, bloom, and tone mapping. One
// box is authored with EmissiveIntensity above 1 — brighter than diffuse white can represent on its
// own — so it blooms convincingly and tone-maps back down without clipping to a flat white blob.
// Effects, and the stack itself, can be toggled at runtime to compare against the
// un-post-processed scene. See docs/post-processing.md and docs/pbr.md.
//
// Controls: B toggle bloom, V toggle vignette, T toggle tone-map operator (ACES/Reinhard),
// P toggle the whole stack, ESC exit.

using var window = Window.Create();
var world = new World();

// Not `using var`: GL-owning objects must be disposed from OnClosing, while the context is still
// alive, not via top-level `using` declarations that dispose after window.Run() returns — by then
// the context is already torn down and the first not-yet-resolved GL call in Dispose() throws
// SymbolLoadingException instead of cleaning up (see Window.OnClosing's remarks and issue #207).
var registry = new GpuMeshRegistry(window.Gl);
var textures = new TextureManager(window.Gl);

static Material3D Matte(Color diffuse) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color(
            (byte)(diffuse.R * 0.2f),
            (byte)(diffuse.G * 0.2f),
            (byte)(diffuse.B * 0.2f)
        ),
        Diffuse = diffuse,
        Specular = new Color(20, 20, 20),
        Shininess = 24f,
    };

// Emissive materials write bright, above-threshold colour straight into the bloom pass regardless
// of scene lighting, the same trick CornellBox's light panel uses.
static Material3D Emissive(Color color) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = color,
        Diffuse = color,
        Specular = Color.White,
        Shininess = 0f,
    };

void AddBox(Vector3 position, Vector3 scale, Material3D material)
{
    var meshData = MeshFactory.CreateBox("box");
    var entity = world.CreateEntity();
    world.AddComponent(entity, registry.Register(meshData));
    world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, scale));
    world.AddComponent(entity, material);
    world.AddComponent(entity, meshData.ToAabb());
}

// Dark floor so the emissive boxes read clearly and the vignette's edge-darkening is visible
// against something other than pure black.
AddBox(new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 14f), Matte(new Color(40, 40, 45)));

// A cluster of brightly emissive boxes at different heights/colours — the bloom pass's threshold
// picks these up and glows them; the matte boxes beside them stay unaffected, showing the
// threshold at work.
AddBox(new Vector3(-3f, 1f, 0f), new Vector3(1f), Emissive(new Color(255, 60, 60)));
AddBox(new Vector3(0f, 1.6f, -1f), new Vector3(1f), Emissive(new Color(60, 255, 120)));
AddBox(new Vector3(3f, 1f, 0f), new Vector3(1f), Emissive(new Color(80, 140, 255)));
AddBox(new Vector3(0f, 0.5f, 2.5f), new Vector3(1.5f, 0.5f, 1f), Emissive(new Color(255, 220, 80)));
AddBox(new Vector3(-1.5f, 0.5f, 1.5f), new Vector3(0.8f), Matte(new Color(150, 150, 160)));
AddBox(new Vector3(1.5f, 0.5f, 1.5f), new Vector3(0.8f), Matte(new Color(150, 150, 160)));

// Authored several times brighter than diffuse white via EmissiveIntensity — Color's byte-based
// channels can't represent that on their own. Only reads as a genuine light source (rather than
// clamping to flat white) because the pipeline below runs HDR end to end: Renderer3D writes linear
// HDR colour, PostProcessStack keeps it unclamped through bloom, and ToneMapEffect compresses the
// result back down for the backbuffer.
AddBox(
    new Vector3(0f, 2.6f, -1f),
    new Vector3(0.6f),
    Emissive(Color.White) with
    {
        UsePbr = true,
        EmissiveColor = Color.White,
        EmissiveIntensity = 6f,
    }
);

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, 4f, 9f),
        Target: new Vector3(0f, 0.5f, 0f),
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: 100f
    )
);

// Kept dim so the emissive boxes (not lit geometry) are what triggers bloom's threshold.
world.AddComponent(
    world.CreateEntity("light"),
    new DirectionalLight
    {
        Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.4f)),
        Color = Color.White,
        Intensity = 0.35f,
    }
);

// hdrOutput: true makes the PBR path write linear HDR colour (skipping its usual in-shader
// Reinhard tone-map/gamma-encode) so PostProcessStack/ToneMapEffect below do that compression once,
// over the whole frame, instead — see docs/pbr.md's HDR section.
var renderer3D = new Renderer3D(window.Gl, hdrOutput: true);
var meshRenderSystem = new MeshRenderSystem(renderer3D, registry, textures, world, window);

// hdr: true allocates the scene/ping-pong targets as floating-point Rgba16F instead of the
// original 8-bit Rgba8, so colour values above 1.0 (the above-white emissive box) survive through
// the chain instead of clamping the instant they're written. Effect chain: vignette/colour-grade,
// then bloom (multi-pass, its own buffers matched to Rgba16F too), then tone mapping — which must
// be last, since it's what compresses the HDR result back down for the backbuffer (enforced by
// PostProcessPlanner.ValidateOrdering). See docs/post-processing.md's "HDR and tone mapping".
var postProcessStack = new PostProcessStack(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    sceneHasDepth: true,
    hdr: true
);
var vignette = new VignetteEffect(window.Gl) { Intensity = 0.5f, Saturation = 1.1f };
var bloom = new BloomEffect(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    RenderTargetFormat.Rgba16F
)
{
    Threshold = 1f,
    Intensity = 1.2f,
};
var toneMap = new ToneMapEffect(window.Gl) { Operator = ToneMapOperator.AcesFilmic };
postProcessStack.Effects.Add(vignette);
postProcessStack.Effects.Add(bloom);
postProcessStack.Effects.Add(toneMap);

window.OnResize += size => postProcessStack.Resize((int)size.X, (int)size.Y);
window.OnClosing += () =>
{
    toneMap.Dispose();
    bloom.Dispose();
    vignette.Dispose();
    postProcessStack.Dispose();
    renderer3D.Dispose();
    textures.Dispose();
    registry.Dispose();
};

Console.WriteLine("Post-Processing Demo (HDR)");
Console.WriteLine(
    "B: toggle bloom | V: toggle vignette | T: toggle tone-map operator | P: toggle stack | ESC: exit"
);

Keyboard.AddKeyDown(
    Keys.B,
    () =>
    {
        bloom.Enabled = !bloom.Enabled;
        Console.WriteLine($"Bloom {(bloom.Enabled ? "ON" : "OFF")}");
    }
);
Keyboard.AddKeyDown(
    Keys.V,
    () =>
    {
        vignette.Enabled = !vignette.Enabled;
        Console.WriteLine($"Vignette {(vignette.Enabled ? "ON" : "OFF")}");
    }
);
Keyboard.AddKeyDown(
    Keys.T,
    () =>
    {
        toneMap.Operator =
            toneMap.Operator == ToneMapOperator.AcesFilmic
                ? ToneMapOperator.Reinhard
                : ToneMapOperator.AcesFilmic;
        Console.WriteLine($"Tone-map operator: {toneMap.Operator}");
    }
);
Keyboard.AddKeyDown(
    Keys.P,
    () =>
    {
        postProcessStack.Enabled = !postProcessStack.Enabled;
        Console.WriteLine(
            $"Post-processing stack {(postProcessStack.Enabled ? "ON" : "OFF")}"
                + (
                    postProcessStack.Enabled
                        ? ""
                        : " (renderer3D is in HDR mode, so the scene now writes"
                            + " unclamped/un-gamma-corrected colour straight to the backbuffer —"
                            + " this is why the stack normally owns tone mapping)"
                )
        );
    }
);
Keyboard.AddKeyDown(Keys.Escape, window.Close);

var elapsed = 0f;
var cameraStore = world.GetStore<Camera3D>();

window.OnUpdate += delta =>
{
    elapsed += (float)delta;
    // Slow orbit so the bloom glow and vignette darkening are visible from every angle.
    var angle = elapsed * 0.15f;
    if (cameraStore.TryGet(cameraEntity, out var camera))
    {
        var offset = new Vector3(MathF.Sin(angle), 0.45f, MathF.Cos(angle)) * 9f;
        world.AddComponent(cameraEntity, camera with { Position = offset });
    }
};

window.OnRender += delta =>
{
    // MeshRenderSystem needs no changes to work with the stack: it simply renders while the
    // stack's offscreen scene target is already bound.
    postProcessStack.Render(meshRenderSystem.Render);

    // Opt-in headless screenshot hook (see Samples/TextRenderingExample): set YAEGER_SCREENSHOT
    // to a file path to capture the first rendered frame as a PNG and exit — handy for
    // showcasing/reproducing rendering bugs (e.g. from a CI run or a headless Xvfb session)
    // without a human watching the window.
    var screenshotPath = Environment.GetEnvironmentVariable("YAEGER_SCREENSHOT");
    if (screenshotPath is not null)
    {
        ScreenshotCapture.SaveFramebufferPng(window, screenshotPath);
        Console.WriteLine($"Screenshot saved to {screenshotPath}");
        window.Close();
    }
};

window.Run();

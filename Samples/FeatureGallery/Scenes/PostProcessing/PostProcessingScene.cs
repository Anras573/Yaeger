using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.PostProcessing;

/// <summary>
/// Post-processing demo (issues #151, #193): a dim scene with a handful of emissive "light" boxes,
/// run through an HDR PostProcessStack chaining vignette/colour-grade, bloom, and tone mapping. One
/// box is authored with EmissiveIntensity above 1 — brighter than diffuse white can represent on
/// its own — so it blooms convincingly and tone-maps back down without clipping to a flat white
/// blob. Effects, and the stack itself, can be toggled at runtime to compare against the
/// un-post-processed scene. See docs/post-processing.md and docs/pbr.md.
/// </summary>
public sealed class PostProcessingScene : IDemoScene
{
    private World? _world;
    private GpuMeshRegistry? _registry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private PostProcessStack? _postProcessStack;
    private VignetteEffect? _vignette;
    private BloomEffect? _bloom;
    private ToneMapEffect? _toneMap;
    private Entity _cameraEntity;
    private ComponentStorage<Camera3D>? _cameraStore;
    private float _elapsed;
    private Window? _window;
    private bool _screenshotTaken;

    public string Name => "Post-Processing";
    public string Description => "HDR bloom, vignette, and tone mapping over an emissive scene";
    public string Controls => "B: bloom, V: vignette, T: tone-map operator, P: toggle stack";

    public void Load(Window window)
    {
        _window = window;
        _screenshotTaken = false;

        var world = new World();
        _world = world;
        var registry = new GpuMeshRegistry(window.Gl);
        _registry = registry;
        var textures = new TextureManager(window.Gl);
        _textures = textures;

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

        // Emissive materials write bright, above-threshold colour straight into the bloom pass
        // regardless of scene lighting, the same trick CornellBox's light panel uses.
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

        // Dark floor so the emissive boxes read clearly and the vignette's edge-darkening is
        // visible against something other than pure black.
        AddBox(new Vector3(0f, -0.5f, 0f), new Vector3(14f, 1f, 14f), Matte(new Color(40, 40, 45)));

        // A cluster of brightly emissive boxes at different heights/colours — the bloom pass's
        // threshold picks these up and glows them; the matte boxes beside them stay unaffected,
        // showing the threshold at work.
        AddBox(new Vector3(-3f, 1f, 0f), new Vector3(1f), Emissive(new Color(255, 60, 60)));
        AddBox(new Vector3(0f, 1.6f, -1f), new Vector3(1f), Emissive(new Color(60, 255, 120)));
        AddBox(new Vector3(3f, 1f, 0f), new Vector3(1f), Emissive(new Color(80, 140, 255)));
        AddBox(
            new Vector3(0f, 0.5f, 2.5f),
            new Vector3(1.5f, 0.5f, 1f),
            Emissive(new Color(255, 220, 80))
        );
        AddBox(new Vector3(-1.5f, 0.5f, 1.5f), new Vector3(0.8f), Matte(new Color(150, 150, 160)));
        AddBox(new Vector3(1.5f, 0.5f, 1.5f), new Vector3(0.8f), Matte(new Color(150, 150, 160)));

        // Authored several times brighter than diffuse white via EmissiveIntensity — Color's
        // byte-based channels can't represent that on their own. Only reads as a genuine light
        // source (rather than clamping to flat white) because the pipeline below runs HDR end to
        // end: Renderer3D writes linear HDR colour, PostProcessStack keeps it unclamped through
        // bloom, and ToneMapEffect compresses the result back down for the backbuffer.
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
        _cameraEntity = cameraEntity;
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

        // hdrOutput: true makes the PBR path write linear HDR colour (skipping its usual
        // in-shader Reinhard tone-map/gamma-encode) so PostProcessStack/ToneMapEffect below do
        // that compression once, over the whole frame, instead — see docs/pbr.md's HDR section.
        var renderer3D = new Renderer3D(window.Gl, hdrOutput: true);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(renderer3D, registry, textures, world, window);

        // hdr: true allocates the scene/ping-pong targets as floating-point Rgba16F instead of
        // the original 8-bit Rgba8, so colour values above 1.0 (the above-white emissive box)
        // survive through the chain instead of clamping the instant they're written. Effect
        // chain: vignette/colour-grade, then bloom (multi-pass, its own buffers matched to
        // Rgba16F too), then tone mapping — which must be last, since it's what compresses the
        // HDR result back down for the backbuffer (enforced by PostProcessPlanner.ValidateOrdering).
        // See docs/post-processing.md's "HDR and tone mapping".
        var postProcessStack = new PostProcessStack(
            window.Gl,
            (int)window.Size.X,
            (int)window.Size.Y,
            sceneHasDepth: true,
            hdr: true
        );
        _postProcessStack = postProcessStack;
        var vignette = new VignetteEffect(window.Gl) { Intensity = 0.5f, Saturation = 1.1f };
        _vignette = vignette;
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
        _bloom = bloom;
        var toneMap = new ToneMapEffect(window.Gl) { Operator = ToneMapOperator.AcesFilmic };
        _toneMap = toneMap;
        postProcessStack.Effects.Add(vignette);
        postProcessStack.Effects.Add(bloom);
        postProcessStack.Effects.Add(toneMap);

        _cameraStore = world.GetStore<Camera3D>();
        _elapsed = 0f;

        Console.WriteLine("Post-Processing Demo (HDR)");
        Console.WriteLine(
            "B: toggle bloom | V: toggle vignette | T: toggle tone-map operator | P: toggle stack"
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
                                    + " unclamped/un-gamma-corrected colour straight to the"
                                    + " backbuffer — this is why the stack normally owns tone"
                                    + " mapping)"
                        )
                );
            }
        );
    }

    public void Update(float deltaTime)
    {
        _elapsed += deltaTime;
        // Slow orbit so the bloom glow and vignette darkening are visible from every angle.
        var angle = _elapsed * 0.15f;
        if (_cameraStore is { } store && store.TryGet(_cameraEntity, out var camera))
        {
            var offset = new Vector3(MathF.Sin(angle), 0.45f, MathF.Cos(angle)) * 9f;
            _world?.AddComponent(_cameraEntity, camera with { Position = offset });
        }
    }

    public void Render(float deltaTime)
    {
        // MeshRenderSystem needs no changes to work with the stack: it simply renders while the
        // stack's offscreen scene target is already bound.
        if (_meshRenderSystem is { } meshRenderSystem)
            _postProcessStack?.Render(meshRenderSystem.Render);

        // Opt-in headless screenshot hook: set YAEGER_SCREENSHOT to a file path to capture the
        // first rendered frame as a PNG and exit — handy for showcasing/reproducing rendering
        // bugs (e.g. from a CI run or a headless Xvfb session) without a human watching the
        // window.
        if (_screenshotTaken || _window is null)
            return;

        var screenshotPath = Environment.GetEnvironmentVariable("YAEGER_SCREENSHOT");
        if (screenshotPath is null)
            return;

        ScreenshotCapture.SaveFramebufferPng(_window, screenshotPath);
        Console.WriteLine($"Screenshot saved to {screenshotPath}");
        _screenshotTaken = true;
        _window.Close();
    }

    public void Resize(Vector2 size) => _postProcessStack?.Resize((int)size.X, (int)size.Y);

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.B);
        Keyboard.RemoveKeyDown(Keys.V);
        Keyboard.RemoveKeyDown(Keys.T);
        Keyboard.RemoveKeyDown(Keys.P);

        _toneMap?.Dispose();
        _bloom?.Dispose();
        _vignette?.Dispose();
        _postProcessStack?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _registry?.Dispose();
    }
}

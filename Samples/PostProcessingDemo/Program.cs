using System.Numerics;
using PostProcessingDemo;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Post-processing demo (issue #151): a dim scene with a handful of emissive "light" boxes, run
// through a PostProcessStack chaining vignette/colour-grade and bloom. Both effects, and the stack
// itself, can be toggled at runtime to compare against the un-post-processed scene. See
// docs/post-processing.md.
//
// Controls: B toggle bloom, V toggle vignette, P toggle the whole stack, ESC exit.

using var window = Window.Create();
var world = new World();
using var registry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

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

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, registry, textures, world, window);

// Effect chain: vignette/colour-grade first (cheap, single pass), then bloom (multi-pass) —
// PostProcessStack ping-pongs the offscreen targets between them per docs/post-processing.md.
using var postProcessStack = new PostProcessStack(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    sceneHasDepth: true
);
using var vignette = new VignetteEffect(window.Gl) { Intensity = 0.5f, Saturation = 1.1f };
using var bloom = new BloomEffect(window.Gl, (int)window.Size.X, (int)window.Size.Y)
{
    Threshold = 0.8f,
    Intensity = 1.2f,
};
postProcessStack.Effects.Add(vignette);
postProcessStack.Effects.Add(bloom);

window.OnResize += size => postProcessStack.Resize((int)size.X, (int)size.Y);

Console.WriteLine("Post-Processing Demo");
Console.WriteLine("B: toggle bloom | V: toggle vignette | P: toggle stack | ESC: exit");

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
    Keys.P,
    () =>
    {
        postProcessStack.Enabled = !postProcessStack.Enabled;
        Console.WriteLine($"Post-processing stack {(postProcessStack.Enabled ? "ON" : "OFF")}");
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
};

window.Run();

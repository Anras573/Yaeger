using System.Numerics;
using Sponza;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Sponza demo — loads the Intel/KhronosGroup Sponza glTF via AssimpLoader.
// The glTF materials are rendered through Renderer3D's PBR metallic/roughness path
// (AssimpLoader flags them as PBR automatically — see docs/pbr.md).
// Assets are fetched automatically on first build via the FetchSponzaAssets MSBuild target.
// Requires native libassimp at runtime (e.g. apt install libassimp-dev on Linux).
// Controls: WASD move, Q/E up/down, right-mouse-drag look, ESC exit.

var modelPath = AssetPath.Resolve("Assets/Sponza/Sponza.gltf");

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine(
        $"Model not found: {modelPath}\n"
            + "Run 'dotnet build' outside CI to fetch assets automatically."
    );
    return;
}

var modelScene = AssimpLoader.LoadScene(modelPath);
Console.WriteLine($"Loaded {modelScene.Meshes.Count} mesh(es) from {modelPath}");

using var window = Window.Create();

var world = new World();
using var registry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

foreach (var modelMesh in modelScene.Meshes)
{
    var entity = string.IsNullOrWhiteSpace(modelMesh.Name)
        ? world.CreateEntity()
        : world.CreateEntity(modelMesh.Name);
    var handle = registry.Register(modelMesh.Mesh);
    world.AddComponent(entity, handle);
    world.AddComponent(entity, modelMesh.Transform);
    world.AddComponent(entity, Material3D.FromModel(modelMesh.Material));
    world.AddComponent(entity, modelMesh.Mesh.ToAabb());
}

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, 5f, 4f),
        Target: Vector3.Zero,
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: 500f
    )
);

var lightEntity = world.CreateEntity("light");
world.AddComponent(
    lightEntity,
    new DirectionalLight
    {
        Direction = Vector3.Normalize(new Vector3(1f, 4f, 0f)),
        Color = Color.White,
        Intensity = 1f,
    }
);

using var renderer3D = new Renderer3D(window.Gl);
using var shadowMapRenderer = new ShadowMapRenderer(
    window.Gl,
    new ShadowSettings
    {
        MapResolution = 2048,
        OrthographicSize = 25f,
        NearPlane = 0.1f,
        FarPlane = 500f,
        Bias = 0.004f,
        EnablePcf = true,
    }
);
var meshRenderSystem = new MeshRenderSystem(
    renderer3D,
    registry,
    textures,
    world,
    window,
    shadowMapRenderer: shadowMapRenderer
);
var freeFlySystem = new FreeFlySystem(world, cameraEntity);

using var inspector = new ImGuiInspector(window, world);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);

window.OnUpdate += deltaTime => freeFlySystem.Update((float)deltaTime);
window.OnRender += delta =>
{
    meshRenderSystem.Render();
    inspector.Render(delta);
};

window.Run();

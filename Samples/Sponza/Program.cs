using System.Numerics;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Sponza demo — loads the Intel/KhronosGroup Sponza glTF via AssimpLoader.
// Place the model at Assets/Sponza/Sponza.gltf (download from KhronosGroup/glTF-Sample-Assets).
// Requires native libassimp at runtime (e.g. apt install libassimp-dev on Linux).
// Press ESC to exit.

var modelPath = AssetPath.Resolve("Assets/Sponza/Sponza.gltf");

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine(
        $"Model not found: {modelPath}\n"
            + "Download from https://github.com/KhronosGroup/glTF-Sample-Assets and place it at that path."
    );
    return;
}

var modelScene = AssimpLoader.LoadScene(modelPath);
Console.WriteLine($"Loaded {modelScene.Meshes.Count} mesh(es) from {modelPath}");

using var window = Window.Create();

var world = new World();
var registry = new GpuMeshRegistry(window.Gl);
var textures = new TextureManager(window.Gl);

foreach (var modelMesh in modelScene.Meshes)
{
    var entity = string.IsNullOrWhiteSpace(modelMesh.Name)
        ? world.CreateEntity()
        : world.CreateEntity(modelMesh.Name);
    var handle = registry.Register(modelMesh.Mesh);
    world.AddComponent(entity, handle);
    world.AddComponent(entity, modelMesh.Transform);
    world.AddComponent(entity, Material3D.FromModel(modelMesh.Material));
}

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, 5f, 15f),
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
        Direction = Vector3.Normalize(new Vector3(-1f, -1f, -1f)),
        Color = Color.White,
        Intensity = 1f,
    }
);

var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, registry, textures, world, window);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnRender += _ => meshRenderSystem.Render();

window.Run();

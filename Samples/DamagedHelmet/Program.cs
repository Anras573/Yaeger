using System.Numerics;
using DamagedHelmet;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// DamagedHelmet demo — loads the KhronosGroup DamagedHelmet glTF via AssimpLoader and renders it
// through Renderer3D's PBR metallic/roughness path, lit by a directional sun plus two point
// lights, inside a procedurally generated sky cubemap (see ProceduralSkybox). The metallic parts
// of the helmet also reflect that sky via image-based lighting (IblPrefilter/EnvironmentMapRegistry).
// Assets are fetched automatically on first build via the FetchDamagedHelmetAssets MSBuild target.
// Requires native libassimp at runtime (e.g. apt install libassimp-dev on Linux).
// Controls: camera orbits the helmet on its own — left-mouse-drag to orbit manually,
// scroll to zoom, Space to pause/resume the auto-orbit, F1 inspector, ESC exit.

var modelPath = AssetPath.Resolve("Assets/DamagedHelmet/DamagedHelmet.gltf");

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

// Track world-space scene bounds while spawning entities so the orbit camera can frame the
// helmet regardless of the model's authored scale.
var sceneMin = new Vector3(float.MaxValue);
var sceneMax = new Vector3(float.MinValue);

foreach (var modelMesh in modelScene.Meshes)
{
    var entity = string.IsNullOrWhiteSpace(modelMesh.Name)
        ? world.CreateEntity()
        : world.CreateEntity(modelMesh.Name);
    var handle = registry.Register(modelMesh.Mesh);
    var aabb = modelMesh.Mesh.ToAabb();
    world.AddComponent(entity, handle);
    world.AddComponent(entity, modelMesh.Transform);
    world.AddComponent(entity, Material3D.FromModel(modelMesh.Material));
    world.AddComponent(entity, aabb);

    var modelMatrix = modelMesh.Transform.ModelMatrix;
    for (var corner = 0; corner < 8; corner++)
    {
        var local = new Vector3(
            (corner & 1) == 0 ? aabb.Min.X : aabb.Max.X,
            (corner & 2) == 0 ? aabb.Min.Y : aabb.Max.Y,
            (corner & 4) == 0 ? aabb.Min.Z : aabb.Max.Z
        );
        var worldCorner = Vector3.Transform(local, modelMatrix);
        sceneMin = Vector3.Min(sceneMin, worldCorner);
        sceneMax = Vector3.Max(sceneMax, worldCorner);
    }
}

var sceneCenter = (sceneMin + sceneMax) / 2f;
var sceneExtent = sceneMax - sceneMin;
var orbitRadius = MathF.Max(sceneExtent.X, MathF.Max(sceneExtent.Y, sceneExtent.Z)) * 1.8f;

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: sceneCenter + new Vector3(0f, orbitRadius * 0.35f, orbitRadius),
        Target: sceneCenter,
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: orbitRadius * 0.01f,
        Far: orbitRadius * 50f
    )
);

// The sun both lights the scene and is painted into the procedural skybox, so the bright spot
// in the sky matches where the key light actually comes from.
var sunDirection = Vector3.Normalize(new Vector3(0.45f, 0.65f, 0.35f));

var sunEntity = world.CreateEntity("sun");
world.AddComponent(
    sunEntity,
    new DirectionalLight
    {
        Direction = sunDirection,
        Color = new Color(255, 245, 230),
        Intensity = 1.1f,
    }
);

// Warm fill from the front-left, cool rim from behind — classic three-point-ish setup.
var fillLight = world.CreateEntity("fill light");
world.AddComponent(
    fillLight,
    new Transform3D(
        sceneCenter + new Vector3(-orbitRadius, orbitRadius * 0.4f, orbitRadius),
        Quaternion.Identity,
        Vector3.One
    )
);
world.AddComponent(
    fillLight,
    new PointLight
    {
        Color = new Color(255, 217, 179),
        Intensity = 0.6f,
        Range = orbitRadius * 6f,
    }
);

var rimLight = world.CreateEntity("rim light");
world.AddComponent(
    rimLight,
    new Transform3D(
        sceneCenter + new Vector3(orbitRadius * 0.6f, orbitRadius * 0.8f, -orbitRadius),
        Quaternion.Identity,
        Vector3.One
    )
);
world.AddComponent(
    rimLight,
    new PointLight
    {
        Color = new Color(153, 191, 255),
        Intensity = 0.8f,
        Range = orbitRadius * 6f,
    }
);

using var cubemaps = new CubemapRegistry(window.Gl);
using var skyboxRenderer = new SkyboxRenderer(window.Gl);

var skyboxFaces = ProceduralSkybox.GenerateFaces(
    Path.Combine(AppContext.BaseDirectory, "Assets", "GeneratedSkybox"),
    sunDirection
);
var skyboxHandle = cubemaps.Register(
    skyboxFaces[0],
    skyboxFaces[1],
    skyboxFaces[2],
    skyboxFaces[3],
    skyboxFaces[4],
    skyboxFaces[5]
);
world.AddComponent(world.CreateEntity("skybox"), skyboxHandle);

// Prefilter the skybox once so the PBR helmet is lit and reflected by the sky instead of a flat
// ambient term — see docs/pbr.md#image-based-lighting.
using var iblPrefilter = new IblPrefilter(window.Gl);
using var environmentMaps = new EnvironmentMapRegistry(cubemaps, iblPrefilter);
environmentMaps.Register(skyboxHandle, (int)window.Size.X, (int)window.Size.Y);

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(
    renderer3D,
    registry,
    textures,
    world,
    window,
    skyboxRenderer: skyboxRenderer,
    cubemapRegistry: cubemaps,
    environmentMaps: environmentMaps
);
var orbitCameraSystem = new OrbitCameraSystem(world, cameraEntity, sceneCenter, orbitRadius);

using var inspector = new ImGuiInspector(window, world);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
Keyboard.AddKeyDown(Keys.Space, orbitCameraSystem.ToggleAutoOrbit);

window.OnUpdate += deltaTime => orbitCameraSystem.Update((float)deltaTime);
window.OnRender += delta =>
{
    meshRenderSystem.Render();
    inspector.Render(delta);
};

window.Run();

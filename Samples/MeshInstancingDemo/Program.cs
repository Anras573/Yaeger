using System.Numerics;
using MeshInstancingDemo;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Instanced-rendering stress test (issue #148): spawns a large grid of boxes sharing one of two
// (mesh, material) combinations, so MeshRenderSystem collapses the whole grid into a small, constant
// number of GL draw calls instead of one per entity. See docs/instancing.md.
//
// Controls: I toggle instancing on/off (compare the draw-call counter and FPS), ESC exit.

const int gridX = 20;
const int gridY = 10;
const int gridZ = 10;
const float spacing = 1.5f;
const int totalInstances = gridX * gridY * gridZ;

using var window = Window.Create();
var world = new World();
using var registry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

var boxData = MeshFactory.CreateBox("box");
var boxMesh = registry.Register(boxData);
var boxAabb = boxData.ToAabb();

static Material3D Matte(Color diffuse) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color(
            (byte)(diffuse.R * 0.3f),
            (byte)(diffuse.G * 0.3f),
            (byte)(diffuse.B * 0.3f)
        ),
        Diffuse = diffuse,
        Specular = new Color(20, 20, 20),
        Shininess = 24f,
    };

// Two materials sharing the same mesh: MeshRenderSystem groups by (mesh, material), so the grid
// below collapses into two instanced draw calls for the main pass — and just one for the shadow
// pass, which groups by mesh alone since depth doesn't depend on material.
var red = Matte(new Color(200, 60, 60));
var blue = Matte(new Color(60, 90, 200));

var sceneCenter =
    new Vector3((gridX - 1) * spacing, (gridY - 1) * spacing, (gridZ - 1) * spacing) * 0.5f;

for (var x = 0; x < gridX; x++)
for (var y = 0; y < gridY; y++)
for (var z = 0; z < gridZ; z++)
{
    var position = new Vector3(x, y, z) * spacing;
    var material = (x + y + z) % 2 == 0 ? red : blue;

    var entity = world.CreateEntity();
    world.AddComponent(entity, boxMesh);
    world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, new Vector3(0.9f)));
    world.AddComponent(entity, material);
    world.AddComponent(entity, boxAabb);
}

// Camera slowly orbits the whole grid so the instanced draw is visible from every side.
var orbitRadius = MathF.Max(gridX, MathF.Max(gridY, gridZ)) * spacing * 1.6f;
var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: sceneCenter + new Vector3(0f, orbitRadius * 0.4f, orbitRadius),
        Target: sceneCenter,
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: orbitRadius * 10f
    )
);

world.AddComponent(
    world.CreateEntity("light"),
    new DirectionalLight
    {
        Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.4f)),
        Color = Color.White,
        Intensity = 1.1f,
    }
);

using var renderer3D = new Renderer3D(window.Gl);

// Shadow settings sized to frame the whole grid, so MeshRenderSystem's own shadow-pass instanced
// grouping (by mesh only) is exercised too, not just the main pass.
using var shadowMapRenderer = new ShadowMapRenderer(
    window.Gl,
    new ShadowSettings
    {
        MapResolution = 1024,
        OrthographicSize = orbitRadius,
        NearPlane = 0.1f,
        FarPlane = orbitRadius * 4f,
        Bias = 0.005f,
        EnablePcf = false,
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

Console.WriteLine($"Mesh Instancing Demo - {totalInstances} boxes, 2 shared materials");
Console.WriteLine("Press I to toggle instancing, ESC to exit");

var instancingEnabled = true;
Keyboard.AddKeyDown(
    Keys.I,
    () =>
    {
        instancingEnabled = !instancingEnabled;
        // A threshold of int.MaxValue means no group ever qualifies, so every instance falls back
        // to the pre-#148 one-draw-call-per-entity path — the comparison this demo exists to show.
        meshRenderSystem.InstancingThreshold = instancingEnabled ? 4 : int.MaxValue;
        Console.WriteLine($"Instancing {(instancingEnabled ? "ON" : "OFF")}");
    }
);
Keyboard.AddKeyDown(Keys.Escape, window.Close);

var elapsed = 0f;
var frameCount = 0;
var secondElapsed = 0.0;
var cameraStore = world.GetStore<Camera3D>();

window.OnUpdate += delta =>
{
    elapsed += (float)delta;
    var angle = elapsed * 0.2f;
    if (cameraStore.TryGet(cameraEntity, out var camera))
    {
        var offset = new Vector3(MathF.Sin(angle), 0.4f, MathF.Cos(angle)) * orbitRadius;
        world.AddComponent(cameraEntity, camera with { Position = sceneCenter + offset });
    }
};

window.OnRender += delta =>
{
    frameCount++;
    secondElapsed += delta;
    if (secondElapsed >= 1.0)
    {
        Console.WriteLine(
            $"FPS: {frameCount} | draw calls: {renderer3D.DrawCallCount} main + {shadowMapRenderer.DrawCallCount} shadow | {totalInstances} instances"
        );
        frameCount = 0;
        secondElapsed = 0;
    }

    meshRenderSystem.Render();
};

window.Run();

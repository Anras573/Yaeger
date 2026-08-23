using System.Numerics;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// CrowdDemo — instanced skinning stress test (issue #198): spawns a grid of characters that all
// share one mesh, material, and SkeletonHandle but each play the walk clip out of phase, so
// MeshRenderSystem collapses the whole crowd into a small, constant number of GL draw calls instead
// of one per character. See docs/instancing.md#instanced-skinning.
//
// Controls: I toggle instancing on/off (compare the draw-call counter and FPS), ESC exit.

const int gridX = 8;
const int gridZ = 8;
const float spacing = 1.2f;
const int crowdSize = gridX * gridZ;

var modelPath = AssetPath.Resolve("Assets/CesiumMan/CesiumMan.gltf");

if (!File.Exists(modelPath))
{
    Console.Error.WriteLine(
        $"Model not found: {modelPath}\n"
            + "Run 'dotnet build' outside CI to fetch assets automatically."
    );
    return;
}

var modelScene = AssimpLoader.LoadScene(modelPath);
if (modelScene.Skeleton is not { } skeleton)
{
    Console.Error.WriteLine("CesiumMan model has no skeleton — nothing to instance.");
    return;
}

using var window = Window.Create();
var world = new World();
using var meshRegistry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);
var skeletonRegistry = new SkeletonRegistry();

// Registered once and shared by every character in the crowd: the whole point of instanced skinning
// is that N entities can share one SkeletonHandle (and, per submesh, one MeshHandle/Material3D) so
// MeshRenderSystem's MeshInstanceBatcher groups them into a handful of draw calls instead of N.
var skinnedVertices = modelScene.Meshes.SelectMany(m =>
    m.Mesh.Vertices.Select(v => new SkinnedVertex(v.Position, v.BoneIndices, v.BoneWeights))
);
var skeletonHandle = skeletonRegistry.Register(skeleton, modelScene.Animations, skinnedVertices);
var clipName = skeletonRegistry.GetClipNames(skeletonHandle).FirstOrDefault();
var clipDuration =
    clipName != null && skeletonRegistry.TryGetClip(skeletonHandle, clipName, out var clip)
        ? clip.Duration
        : 1f;

Console.WriteLine(
    $"Loaded {modelScene.Meshes.Count} mesh(es), {skeleton.BoneCount} bone(s) — spawning {crowdSize} characters"
);

// Register each submesh's GpuMesh/Material3D once outside the spawn loop: every character reuses the
// same MeshHandle/Material3D for its copy of that submesh, which is what lets them share an instanced
// draw group.
var submeshes = modelScene
    .Meshes.Select(m =>
        (Handle: meshRegistry.Register(m.Mesh), Material: Material3D.FromModel(m.Material))
    )
    .ToList();

var gridCenter = new Vector3((gridX - 1) * spacing, 0f, (gridZ - 1) * spacing) * 0.5f;
var random = new Random(1);

for (var x = 0; x < gridX; x++)
for (var z = 0; z < gridZ; z++)
{
    var position = new Vector3(x * spacing, 0f, z * spacing) - gridCenter;
    // Stagger each character's starting time across the clip so the crowd doesn't move in lockstep —
    // exercising the case that matters for instancing: every instance in the group has its own pose.
    var startTime = clipName != null ? (float)random.NextDouble() * clipDuration : 0f;

    foreach (var (handle, material) in submeshes)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, material);
        // The skinning palette already places vertices in scene space relative to the skeleton root,
        // so each character's world position is applied via Transform3D even though it's "identity"
        // for a single-character sample — here it also carries this character's grid position.
        world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, Vector3.One));
        world.AddComponent(entity, skeletonHandle);
        world.AddComponent(
            entity,
            new AnimationPlayer(clipName, loop: true, speed: 1f) { Time = startTime }
        );
    }
}

var orbitRadius = MathF.Max(gridX, gridZ) * spacing * 1.8f;
var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, orbitRadius * 0.5f, orbitRadius),
        Target: new Vector3(0f, 1f, 0f),
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

// Shadow settings sized to frame the whole crowd, so the shadow pass's own instanced-skinning path
// (ShadowMapRenderer.DrawInstancedSkinned) is exercised too, not just the main pass.
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
    meshRegistry,
    textures,
    world,
    window,
    shadowMapRenderer: shadowMapRenderer
);
var animationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);

Console.WriteLine($"Crowd Demo - {crowdSize} characters, clip '{clipName}'");
Console.WriteLine("Press I to toggle instancing, ESC to exit");

var instancingEnabled = true;
Keyboard.AddKeyDown(
    Keys.I,
    () =>
    {
        instancingEnabled = !instancingEnabled;
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
    animationSystem.Update((float)delta);

    elapsed += (float)delta;
    var angle = elapsed * 0.15f;
    if (cameraStore.TryGet(cameraEntity, out var camera))
    {
        var offset = new Vector3(MathF.Sin(angle), 0.5f, MathF.Cos(angle)) * orbitRadius;
        world.AddComponent(cameraEntity, camera with { Position = offset });
    }
};

window.OnRender += delta =>
{
    frameCount++;
    secondElapsed += delta;
    if (secondElapsed >= 1.0)
    {
        Console.WriteLine(
            $"FPS: {frameCount} | draw calls: {renderer3D.DrawCallCount} main + {shadowMapRenderer.DrawCallCount} shadow | {crowdSize} characters"
        );
        frameCount = 0;
        secondElapsed = 0;
    }

    meshRenderSystem.Render();
};

window.Run();

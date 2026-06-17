using System.Numerics;
using SkinnedMeshDemo;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// SkinnedMeshDemo — loads the KhronosGroup CesiumMan glTF (a rigged, animated humanoid) via
// AssimpLoader and plays its looping walk animation through GPU skinning (Renderer3D bone palette).
// Assets are fetched automatically on first build via the FetchCesiumManAssets MSBuild target.
// Requires native libassimp at runtime (e.g. apt install libassimp-dev on Linux).
// Controls: WASD move, Q/E up/down, right-mouse-drag look, ESC exit.

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
Console.WriteLine(
    $"Loaded {modelScene.Meshes.Count} mesh(es), "
        + $"{(modelScene.Skeleton?.BoneCount ?? 0)} bone(s), "
        + $"{modelScene.Animations.Count} animation(s) from {modelPath}"
);

using var window = Window.Create();

var world = new World();
using var meshRegistry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);
var skeletonRegistry = new SkeletonRegistry();

// Register the shared skeleton + clips once and resolve the first available clip to play.
var skeletonHandle = default(SkeletonHandle);
string? clipName = null;
if (modelScene.Skeleton is { } skeleton)
{
    skeletonHandle = skeletonRegistry.Register(skeleton, modelScene.Animations);
    clipName = skeletonRegistry.GetClipNames(skeletonHandle).FirstOrDefault();
    Console.WriteLine(clipName != null ? $"Playing clip '{clipName}'" : "No animation clips found");
}

var isSkinned = modelScene.Skeleton is not null;

foreach (var modelMesh in modelScene.Meshes)
{
    var entity = string.IsNullOrWhiteSpace(modelMesh.Name)
        ? world.CreateEntity()
        : world.CreateEntity(modelMesh.Name);

    world.AddComponent(entity, meshRegistry.Register(modelMesh.Mesh));
    world.AddComponent(entity, Material3D.FromModel(modelMesh.Material));

    if (isSkinned)
    {
        // The skinning palette already places vertices in scene space (bone world transforms run
        // from the scene root), so the model matrix is identity; the per-vertex skin does the rest.
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(entity, skeletonHandle);
        world.AddComponent(entity, new AnimationPlayer(clipName, loop: true, speed: 1f));
        // Deliberately no Aabb3D: a bind-pose box wouldn't bound the animated mesh, so skip frustum
        // culling for skinned entities.
    }
    else
    {
        world.AddComponent(entity, modelMesh.Transform);
        world.AddComponent(entity, modelMesh.Mesh.ToAabb());
    }
}

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, 1f, 4f),
        Target: new Vector3(0f, 1f, 0f),
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: 100f
    )
);

var lightEntity = world.CreateEntity("light");
world.AddComponent(
    lightEntity,
    new DirectionalLight
    {
        Direction = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f)),
        Color = Color.White,
        Intensity = 1.2f,
    }
);

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, meshRegistry, textures, world, window);
var animationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);
var freeFlySystem = new FreeFlySystem(world, cameraEntity);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnUpdate += deltaTime =>
{
    freeFlySystem.Update((float)deltaTime);
    animationSystem.Update((float)deltaTime);
};
window.OnRender += _ => meshRenderSystem.Render();

window.Run();

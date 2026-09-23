using System.Numerics;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.SkinnedMesh;

/// <summary>
/// Loads the KhronosGroup CesiumMan glTF (a rigged, animated humanoid) via AssimpLoader and plays
/// its looping walk animation through GPU skinning (Renderer3D bone palette). Assets are fetched
/// automatically on first build — see the FetchCesiumManAssets target in FeatureGallery.csproj.
/// Requires native libassimp at runtime (e.g. apt install libassimp-dev on Linux).
/// </summary>
public sealed class SkinnedMeshScene : IDemoScene
{
    private GpuMeshRegistry? _meshRegistry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private SkeletalAnimationSystem? _animationSystem;
    private FreeFlyCameraSystem? _freeFlySystem;

    public string Name => "Skinned Mesh";
    public string Description => "GPU-skinned humanoid playing a looping walk animation";
    public string Controls => "WASD move, Q/E up/down, RMB-drag look";

    public void Load(Window window)
    {
        var world = new World();
        var meshRegistry = new GpuMeshRegistry(window.Gl);
        _meshRegistry = meshRegistry;
        _textures = new TextureManager(window.Gl);
        _renderer3D = new Renderer3D(window.Gl);

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

        world.AddComponent(
            world.CreateEntity("light"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f)),
                Color = Color.White,
                Intensity = 1.2f,
            }
        );

        var modelPath = AssetPath.Resolve("Assets/SkinnedMesh/CesiumMan/CesiumMan.gltf");
        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine(
                $"Model not found: {modelPath}\n"
                    + "Run 'dotnet build' outside CI to fetch assets automatically."
            );
        }
        else
        {
            var modelScene = AssimpLoader.LoadScene(modelPath);
            Console.WriteLine(
                $"Loaded {modelScene.Meshes.Count} mesh(es), "
                    + $"{(modelScene.Skeleton?.BoneCount ?? 0)} bone(s), "
                    + $"{modelScene.Animations.Count} animation(s) from {modelPath}"
            );

            var skeletonRegistry = new SkeletonRegistry();

            // Register the shared skeleton + clips once and resolve the first available clip to
            // play.
            var skeletonHandle = default(SkeletonHandle);
            string? clipName = null;
            if (modelScene.Skeleton is { } skeleton)
            {
                // Feeding the skinned vertex data lets SkeletonRegistry precompute each bone's max
                // influence radius, which SkeletalAnimationSystem then uses to write a per-frame
                // Aabb3D — enabling frustum culling for the entities driven by this skeleton (see
                // docs/skeletal-animation.md).
                var skinnedVertices = modelScene.Meshes.SelectMany(m =>
                    m.Mesh.Vertices.Select(v => new SkinnedVertex(
                        v.Position,
                        v.BoneIndices,
                        v.BoneWeights
                    ))
                );
                skeletonHandle = skeletonRegistry.Register(
                    skeleton,
                    modelScene.Animations,
                    skinnedVertices
                );
                clipName = skeletonRegistry.GetClipNames(skeletonHandle).FirstOrDefault();
                Console.WriteLine(
                    clipName != null ? $"Playing clip '{clipName}'" : "No animation clips found"
                );
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
                    // The skinning palette already places vertices in scene space (bone world
                    // transforms run from the scene root), so the model matrix is identity; the
                    // per-vertex skin does the rest.
                    world.AddComponent(entity, Transform3D.Identity);
                    world.AddComponent(entity, skeletonHandle);
                    world.AddComponent(
                        entity,
                        new AnimationPlayer(clipName, loop: true, speed: 1f)
                    );
                    // No Aabb3D added here: SkeletalAnimationSystem writes one itself each frame,
                    // computed from the resolved bone palette (see the SkinnedVertex registration
                    // above), so it always bounds the current animated pose rather than a stale
                    // bind-pose box.
                }
                else
                {
                    world.AddComponent(entity, modelMesh.Transform);
                    world.AddComponent(entity, modelMesh.Mesh.ToAabb());
                }
            }

            _animationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);
        }

        _meshRenderSystem = new MeshRenderSystem(
            _renderer3D,
            meshRegistry,
            _textures,
            world,
            window
        );
        _freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 10f);
    }

    public void Update(float deltaTime)
    {
        _freeFlySystem?.Update(deltaTime);
        _animationSystem?.Update(deltaTime);
    }

    public void Render(float deltaTime) => _meshRenderSystem?.Render();

    public void Dispose()
    {
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _meshRegistry?.Dispose();
    }
}

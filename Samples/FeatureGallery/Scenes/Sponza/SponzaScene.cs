using System.Numerics;
using Yaeger;
using Yaeger.Assets;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Sponza;

/// <summary>
/// Loads the Intel/KhronosGroup Sponza glTF via AssimpLoader. The glTF materials are rendered
/// through Renderer3D's PBR metallic/roughness path (AssimpLoader flags them as PBR automatically
/// — see docs/pbr.md). Kept as a minimal PBR reference scene — see <c>SponzaNight</c> for a more
/// elaborate lighting showcase built on the same model. Assets are fetched automatically on first
/// build — see the FetchSponzaAssets target in FeatureGallery.csproj. Requires native libassimp at
/// runtime (e.g. apt install libassimp-dev on Linux).
/// </summary>
public sealed class SponzaScene : IDemoScene
{
    private GpuMeshRegistry? _registry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private ShadowMapRenderer? _shadowMapRenderer;
    private MeshRenderSystem? _meshRenderSystem;
    private FreeFlyCameraSystem? _freeFlySystem;
    private ImGuiInspector? _inspector;

    public string Name => "Sponza";
    public string Description => "Minimal PBR reference scene — the Sponza atrium";
    public string Controls => "WASD move, Q/E up/down, RMB-drag look, F1: toggle inspector";

    public void Load(Window window)
    {
        var world = new World();
        var registry = new GpuMeshRegistry(window.Gl);
        _registry = registry;
        _textures = new TextureManager(window.Gl);

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

        world.AddComponent(
            world.CreateEntity("light"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(1f, 4f, 0f)),
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var modelPath = AssetPath.Resolve("Assets/Sponza/Sponza.gltf");
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
            Console.WriteLine($"Loaded {modelScene.Meshes.Count} mesh(es) from {modelPath}");

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
        }

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        var shadowMapRenderer = new ShadowMapRenderer(
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
        _shadowMapRenderer = shadowMapRenderer;

        _meshRenderSystem = new MeshRenderSystem(
            renderer3D,
            registry,
            _textures,
            world,
            window,
            shadowMapRenderer: shadowMapRenderer
        );
        _freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 10f);

        var inspector = new ImGuiInspector(window, world);
        _inspector = inspector;

        Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
    }

    public void Update(float deltaTime) => _freeFlySystem?.Update(deltaTime);

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _inspector?.Render(deltaTime);
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.F1);

        _inspector?.Dispose();
        _shadowMapRenderer?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _registry?.Dispose();
    }
}

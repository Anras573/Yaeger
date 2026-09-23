using System.Numerics;
using Yaeger;
using Yaeger.Assets;
using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.DamagedHelmet;

/// <summary>
/// Loads the KhronosGroup DamagedHelmet glTF via AssimpLoader and renders it through
/// Renderer3D's PBR metallic/roughness path, lit by a directional sun plus two point lights,
/// inside a procedurally generated sky cubemap (see <see cref="ProceduralSkybox"/>). The metallic
/// parts of the helmet also reflect that sky via image-based lighting
/// (IblPrefilter/EnvironmentMapRegistry). A looping hum plays from the helmet via
/// AudioSource3D/AudioSystem — orbit around it (or scroll to zoom) to hear it pan and attenuate as
/// the camera moves. Assets are fetched automatically on first build — see the
/// FetchDamagedHelmetAssets target in FeatureGallery.csproj. Requires native libassimp at runtime
/// (e.g. apt install libassimp-dev on Linux).
/// </summary>
public sealed class DamagedHelmetScene : IDemoScene
{
    private GpuMeshRegistry? _registry;
    private TextureManager? _textures;
    private CubemapRegistry? _cubemaps;
    private SkyboxRenderer? _skyboxRenderer;
    private IblPrefilter? _iblPrefilter;
    private EnvironmentMapRegistry? _environmentMaps;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private OrbitCameraSystem? _orbitCameraSystem;
    private SoundBuffer? _humBuffer;
    private AudioSystem? _audioSystem;
    private ImGuiInspector? _inspector;

    public string Name => "Damaged Helmet";
    public string Description =>
        "PBR + IBL showcase: an orbiting glTF helmet under a procedural sky";
    public string Controls =>
        "LMB-drag: orbit, scroll: zoom, SPACE: pause auto-orbit, F1: toggle inspector";

    public void Load(Window window)
    {
        var world = new World();
        var registry = new GpuMeshRegistry(window.Gl);
        _registry = registry;
        _textures = new TextureManager(window.Gl);

        var modelPath = AssetPath.Resolve("Assets/DamagedHelmet/Model/DamagedHelmet.gltf");
        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine(
                $"Model not found: {modelPath}\n"
                    + "Run 'dotnet build' outside CI to fetch assets automatically."
            );

            var emptyCameraEntity = world.CreateEntity("camera");
            world.AddComponent(
                emptyCameraEntity,
                new Camera3D(
                    Position: new Vector3(0f, 1f, 3f),
                    Target: Vector3.Zero,
                    Up: Vector3.UnitY,
                    Fov: MathF.PI / 4f,
                    Near: 0.1f,
                    Far: 100f
                )
            );
            _renderer3D = new Renderer3D(window.Gl);
            _meshRenderSystem = new MeshRenderSystem(
                _renderer3D,
                registry,
                _textures,
                world,
                window
            );
            return;
        }

        var modelScene = AssimpLoader.LoadScene(modelPath);
        Console.WriteLine($"Loaded {modelScene.Meshes.Count} mesh(es) from {modelPath}");

        // Track world-space scene bounds while spawning entities so the orbit camera can frame
        // the helmet regardless of the model's authored scale.
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

        // The sun both lights the scene and is painted into the procedural skybox, so the bright
        // spot in the sky matches where the key light actually comes from.
        var sunDirection = Vector3.Normalize(new Vector3(0.45f, 0.65f, 0.35f));

        world.AddComponent(
            world.CreateEntity("sun"),
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

        var cubemaps = new CubemapRegistry(window.Gl);
        _cubemaps = cubemaps;
        var skyboxRenderer = new SkyboxRenderer(window.Gl);
        _skyboxRenderer = skyboxRenderer;

        var skyboxFaces = ProceduralSkybox.GenerateFaces(
            Path.Combine(AppContext.BaseDirectory, "Assets", "DamagedHelmet", "GeneratedSkybox"),
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

        // Prefilter the skybox once so the PBR helmet is lit and reflected by the sky instead of a
        // flat ambient term — see docs/pbr.md#image-based-lighting.
        var iblPrefilter = new IblPrefilter(window.Gl);
        _iblPrefilter = iblPrefilter;
        var environmentMaps = new EnvironmentMapRegistry(cubemaps, iblPrefilter);
        _environmentMaps = environmentMaps;
        environmentMaps.Register(skyboxHandle, (int)window.Size.X, (int)window.Size.Y);

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(
            renderer3D,
            registry,
            _textures,
            world,
            window,
            skyboxRenderer: skyboxRenderer,
            cubemapRegistry: cubemaps,
            environmentMaps: environmentMaps
        );
        var orbitCameraSystem = new OrbitCameraSystem(
            world,
            cameraEntity,
            sceneCenter,
            orbitRadius
        );
        _orbitCameraSystem = orbitCameraSystem;

        // A looping hum on the helmet itself — AudioSystem drives its OpenAL source position from
        // this entity's Transform3D and the listener from the orbiting Camera3D above, so
        // panning/attenuation tracks the orbit automatically. Distances are scaled to orbitRadius
        // so the effect is audible across the sample's zoom range (see OrbitCameraSystem's min/max
        // radius).
        var humBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/DamagedHelmet/hum.wav");
        _humBuffer = humBuffer;
        var audioSystem = new AudioSystem(world, window.AudioContext);
        _audioSystem = audioSystem;

        var humEntity = world.CreateEntity("helmet hum");
        world.AddComponent(
            humEntity,
            new Transform3D(sceneCenter, Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(
            humEntity,
            new AudioSource3D(
                humBuffer,
                Loop: true,
                Volume: 0.6f,
                MinDistance: orbitRadius * 0.5f,
                MaxDistance: orbitRadius * 3f,
                RolloffFactor: 1.2f
            )
        );

        var inspector = new ImGuiInspector(window, world);
        _inspector = inspector;

        Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
        Keyboard.AddKeyDown(Keys.Space, orbitCameraSystem.ToggleAutoOrbit);
    }

    public void Update(float deltaTime)
    {
        _orbitCameraSystem?.Update(deltaTime);
        _audioSystem?.Update(deltaTime);
    }

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _inspector?.Render(deltaTime);
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.F1);
        Keyboard.RemoveKeyDown(Keys.Space);

        _inspector?.Dispose();
        _audioSystem?.Dispose();
        _humBuffer?.Dispose();
        _renderer3D?.Dispose();
        _iblPrefilter?.Dispose();
        _skyboxRenderer?.Dispose();
        _cubemaps?.Dispose();
        _textures?.Dispose();
        _registry?.Dispose();
    }
}

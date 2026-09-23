using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Tween;

/// <summary>
/// Tween demo (issue #188): a transform, a light, and a material all animate purely from data —
/// no per-frame math anywhere in this file. TweenSystem reads each Tween component and writes the
/// interpolated value straight into the target entity's component every frame.
///
/// The cube's own Tween slides it back and forth (Transform3DPosition, ping-pong, back easing).
/// Since an entity can only carry one Tween at a time, the cube's emissive glow is driven by a
/// second, otherwise-empty carrier entity whose Tween targets the cube's Material3D — the pattern
/// docs/tweening.md recommends for animating more than one channel on the same entity concurrently.
/// </summary>
public sealed class TweenScene : IDemoScene
{
    private GpuMeshRegistry? _meshRegistry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private TweenSystem? _tweenSystem;

    public string Name => "Tween";
    public string Description => "Data-driven position/colour/light tweens with easing curves";
    public string Controls => "None — a static camera watching the tweens play out";

    public void Load(Window window)
    {
        var world = new World();
        var meshRegistry = new GpuMeshRegistry(window.Gl);
        _meshRegistry = meshRegistry;
        _textures = new TextureManager(window.Gl);

        var boxData = MeshFactory.CreateBox("box");
        var boxMesh = meshRegistry.Register(boxData);
        var boxAabb = boxData.ToAabb();

        // The cube: matte grey base colour, animated by two tweens — one on the cube itself
        // (position), one on a separate carrier entity (emissive colour) — see the file banner
        // above.
        var cube = world.CreateEntity("cube");
        world.AddComponent(cube, boxMesh);
        world.AddComponent(
            cube,
            new Transform3D(new Vector3(-2f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(
            cube,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(40, 40, 45),
                Diffuse = new Color(90, 90, 100),
                Specular = new Color(60, 60, 60),
                Shininess = 24f,
            }
        );
        world.AddComponent(cube, boxAabb);
        world.AddComponent(
            cube,
            Yaeger.Graphics.Tween.Create(
                cube,
                TweenChannel.Transform3DPosition,
                new Vector3(-2f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                duration: 2.5f,
                easing: EasingFunction.BackInOut,
                loopMode: TweenLoopMode.PingPong
            )
        );

        var emissiveCarrier = world.CreateEntity("cubeEmissiveTween");
        world.AddComponent(
            emissiveCarrier,
            Yaeger.Graphics.Tween.Create(
                cube,
                TweenChannel.Material3DEmissiveColor,
                Color.Black,
                new Color(255, 120, 30),
                duration: 1.6f,
                easing: EasingFunction.SineInOut,
                loopMode: TweenLoopMode.Loop
            )
        );

        // A pulsing point light above the cube — PointLightIntensity tweened with a sine ease so
        // the pulse accelerates/decelerates smoothly instead of ticking linearly.
        var light = world.CreateEntity("light");
        world.AddComponent(
            light,
            new Transform3D(new Vector3(0f, 3f, 2f), Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(light, PointLight.Default with { Color = Color.White, Range = 20f });
        world.AddComponent(
            light,
            Yaeger.Graphics.Tween.Create(
                light,
                TweenChannel.PointLightIntensity,
                0.3f,
                3f,
                duration: 1.2f,
                easing: EasingFunction.SineInOut,
                loopMode: TweenLoopMode.PingPong
            )
        );

        // A soft, non-tweened directional light so the cube stays visible during the point
        // light's dim phase.
        world.AddComponent(
            world.CreateEntity("sun"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.4f)),
                Color = Color.White,
                Intensity = 0.4f,
            }
        );

        world.AddComponent(
            world.CreateEntity("camera"),
            new Camera3D(
                Position: new Vector3(0f, 2.5f, 7f),
                Target: Vector3.Zero,
                Up: Vector3.UnitY,
                Fov: MathF.PI / 4f,
                Near: 0.1f,
                Far: 100f
            )
        );

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(
            renderer3D,
            meshRegistry,
            _textures,
            world,
            window
        );
        _tweenSystem = new TweenSystem(world);
    }

    public void Update(float deltaTime) => _tweenSystem?.Update(deltaTime);

    public void Render(float deltaTime) => _meshRenderSystem?.Render();

    public void Dispose()
    {
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _meshRegistry?.Dispose();
    }
}

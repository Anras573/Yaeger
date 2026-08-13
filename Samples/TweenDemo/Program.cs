using System.Numerics;
using TweenDemo;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Tween demo (issue #188): a transform, a light, and a material all animate purely from data —
// no per-frame math anywhere in this file. TweenSystem reads each Tween component and writes the
// interpolated value straight into the target entity's component every frame.
//
// The cube's own Tween slides it back and forth (Transform3DPosition, ping-pong, back easing).
// Since an entity can only carry one Tween at a time, the cube's emissive glow is driven by a
// second, otherwise-empty carrier entity whose Tween targets the cube's Material3D — the pattern
// docs/tweening.md recommends for animating more than one channel on the same entity concurrently.
//
// Controls: ESC exit.

using var window = Window.Create();
var world = new World();
using var meshRegistry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

var boxData = MeshFactory.CreateBox("box");
var boxMesh = meshRegistry.Register(boxData);
var boxAabb = boxData.ToAabb();

// The cube: matte grey base colour, animated by two tweens — one on the cube itself (position),
// one on a separate carrier entity (emissive colour) — see the file banner above.
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
    Tween.Create(
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
    Tween.Create(
        cube,
        TweenChannel.Material3DEmissiveColor,
        Color.Black,
        new Color(255, 120, 30),
        duration: 1.6f,
        easing: EasingFunction.SineInOut,
        loopMode: TweenLoopMode.Loop
    )
);

// A pulsing point light above the cube — PointLightIntensity tweened with a sine ease so the
// pulse accelerates/decelerates smoothly instead of ticking linearly.
var light = world.CreateEntity("light");
world.AddComponent(
    light,
    new Transform3D(new Vector3(0f, 3f, 2f), Quaternion.Identity, Vector3.One)
);
world.AddComponent(light, PointLight.Default with { Color = Color.White, Range = 20f });
world.AddComponent(
    light,
    Tween.Create(
        light,
        TweenChannel.PointLightIntensity,
        0.3f,
        3f,
        duration: 1.2f,
        easing: EasingFunction.SineInOut,
        loopMode: TweenLoopMode.PingPong
    )
);

// A soft, non-tweened directional light so the cube stays visible during the point light's dim
// phase.
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

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, meshRegistry, textures, world, window);
var tweenSystem = new TweenSystem(world);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnUpdate += deltaTime => tweenSystem.Update((float)deltaTime);

window.OnRender += _ => meshRenderSystem.Render();

window.Run();

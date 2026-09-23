using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Sequencing;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Sequence;

/// <summary>
/// Sequence demo (issue #189): a directed, multi-beat cutscene — a lift descends, *then* (after a
/// short settle) its two side doors slide open *in parallel*, *then* a reveal light kicks on — all
/// authored as data via SequenceBuilder. There is no per-frame timer/state-machine code anywhere in
/// this file: SequenceSystem drives the beats, and each beat that moves something uses TweenSystem
/// (issue #188) under the hood via SequenceBuilder.StartTween/WaitForTweenFinished.
/// </summary>
public sealed class SequenceScene : IDemoScene
{
    private World? _world;
    private GpuMeshRegistry? _meshRegistry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private TweenSystem? _tweenSystem;
    private SequenceSystem? _sequenceSystem;
    private object? _cutscene;
    private SequenceHandle _handle;

    private Entity _lift;
    private Entity _leftDoor;
    private Entity _rightDoor;
    private Entity _revealLight;
    private Vector3 _liftTop;
    private Vector3 _liftBottom;
    private Vector3 _leftDoorClosed;
    private Vector3 _rightDoorClosed;

    public string Name => "Sequence";
    public string Description => "A directed, multi-beat cutscene authored as data";
    public string Controls => "SPACE: skip to end   R: restart";

    public void Load(Window window)
    {
        var world = new World();
        _world = world;
        var meshRegistry = new GpuMeshRegistry(window.Gl);
        _meshRegistry = meshRegistry;
        _textures = new TextureManager(window.Gl);

        var boxData = MeshFactory.CreateBox("box");
        var boxMesh = meshRegistry.Register(boxData);
        var boxAabb = boxData.ToAabb();

        Entity MakeBox(string tag, Vector3 position, Vector3 scale, Color ambient, Color diffuse)
        {
            var entity = world.CreateEntity(tag);
            world.AddComponent(entity, boxMesh);
            world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, scale));
            world.AddComponent(
                entity,
                new Material3D
                {
                    DiffuseTexturePath = string.Empty,
                    Ambient = ambient,
                    Diffuse = diffuse,
                    Specular = new Color(60, 60, 60),
                    Shininess = 24f,
                }
            );
            world.AddComponent(entity, boxAabb);
            return entity;
        }

        _liftTop = new Vector3(0f, 3f, 0f);
        _liftBottom = new Vector3(0f, 0f, 0f);
        _lift = MakeBox(
            "lift",
            _liftTop,
            new Vector3(1.6f, 0.4f, 1.6f),
            new Color(22, 22, 25),
            new Color(90, 90, 100)
        );

        _leftDoorClosed = new Vector3(-0.9f, 0f, -1.6f);
        var leftDoorOpen = new Vector3(-2.4f, 0f, -1.6f);
        _leftDoor = MakeBox(
            "leftDoor",
            _leftDoorClosed,
            new Vector3(1.2f, 2f, 0.2f),
            new Color(30, 17, 15),
            new Color(120, 70, 60)
        );

        _rightDoorClosed = new Vector3(0.9f, 0f, -1.6f);
        var rightDoorOpen = new Vector3(2.4f, 0f, -1.6f);
        _rightDoor = MakeBox(
            "rightDoor",
            _rightDoorClosed,
            new Vector3(1.2f, 2f, 0.2f),
            new Color(30, 17, 15),
            new Color(120, 70, 60)
        );

        _revealLight = world.CreateEntity("revealLight");
        world.AddComponent(
            _revealLight,
            new Transform3D(new Vector3(0f, 3f, -1.6f), Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(
            _revealLight,
            PointLight.Default with
            {
                Color = new Color(255, 220, 160),
                Intensity = 0f,
                Range = 20f,
            }
        );

        world.AddComponent(
            world.CreateEntity("sun"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.4f)),
                Color = Color.White,
                Intensity = 0.35f,
            }
        );

        world.AddComponent(
            world.CreateEntity("camera"),
            new Camera3D(
                Position: new Vector3(0f, 2.5f, 7f),
                Target: new Vector3(0f, 1f, -1f),
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
        var sequenceSystem = new SequenceSystem();
        _sequenceSystem = sequenceSystem;

        // Build once; Play() creates fresh progress each time, so R can safely re-play the same
        // definition without rebuilding it.
        var cutscene = new SequenceBuilder(world)
            .StartTween(
                _lift,
                Yaeger.Graphics.Tween.Create(
                    _lift,
                    TweenChannel.Transform3DPosition,
                    _liftTop,
                    _liftBottom,
                    duration: 2f,
                    easing: EasingFunction.CubicInOut
                )
            )
            .WaitForTweenFinished(_lift)
            .WaitForSeconds(0.4f) // a short settle before the doors move
            .Parallel(
                branch =>
                    branch
                        .StartTween(
                            _leftDoor,
                            Yaeger.Graphics.Tween.Create(
                                _leftDoor,
                                TweenChannel.Transform3DPosition,
                                _leftDoorClosed,
                                leftDoorOpen,
                                duration: 1f,
                                easing: EasingFunction.SineInOut
                            )
                        )
                        .WaitForTweenFinished(_leftDoor),
                branch =>
                    branch
                        .StartTween(
                            _rightDoor,
                            Yaeger.Graphics.Tween.Create(
                                _rightDoor,
                                TweenChannel.Transform3DPosition,
                                _rightDoorClosed,
                                rightDoorOpen,
                                duration: 1f,
                                easing: EasingFunction.SineInOut
                            )
                        )
                        .WaitForTweenFinished(_rightDoor)
            )
            .StartTween(
                _revealLight,
                Yaeger.Graphics.Tween.Create(
                    _revealLight,
                    TweenChannel.PointLightIntensity,
                    0f,
                    3f,
                    duration: 0.8f,
                    easing: EasingFunction.SineOut
                )
            )
            .Callback(() => Console.WriteLine("Reveal!"))
            .Build();

        _cutscene = cutscene;
        _handle = sequenceSystem.Play(cutscene);

        Keyboard.AddKeyDown(Keys.Space, () => sequenceSystem.SkipToEnd(_handle));
        Keyboard.AddKeyDown(Keys.R, Restart);
    }

    private void Restart()
    {
        if (
            _world is not { } world
            || _sequenceSystem is not { } sequenceSystem
            || _cutscene is not Yaeger.Sequencing.Sequence cutscene
        )
            return;

        // Snap every animated entity back to its starting pose before replaying — otherwise the
        // new playback's first StartTween would animate from wherever the previous run left off.
        world.AddComponent(
            _lift,
            new Transform3D(
                _liftTop,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(_lift).Scale
            )
        );
        world.AddComponent(
            _leftDoor,
            new Transform3D(
                _leftDoorClosed,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(_leftDoor).Scale
            )
        );
        world.AddComponent(
            _rightDoor,
            new Transform3D(
                _rightDoorClosed,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(_rightDoor).Scale
            )
        );
        world.AddComponent(
            _revealLight,
            world.GetComponent<PointLight>(_revealLight) with
            {
                Intensity = 0f,
            }
        );
        _handle = sequenceSystem.Play(cutscene);
    }

    public void Update(float deltaTime)
    {
        _sequenceSystem?.Update(deltaTime);
        _tweenSystem?.Update(deltaTime);
    }

    public void Render(float deltaTime) => _meshRenderSystem?.Render();

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.Space);
        Keyboard.RemoveKeyDown(Keys.R);

        _renderer3D?.Dispose();
        _textures?.Dispose();
        _meshRegistry?.Dispose();
    }
}

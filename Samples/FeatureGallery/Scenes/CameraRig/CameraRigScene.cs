using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.CameraRig;

/// <summary>
/// Camera Rig demo (issue #273): a cart drives around a rectangular loop via
/// <see cref="PathFollow3D"/>, and the same <see cref="Camera3D"/> entity is driven three
/// different ways depending on the selected mode — the three <see cref="CameraRigSystem"/>
/// features that no other sample exercises. <c>1</c>/<c>2</c>/<c>3</c> switch modes; <c>Space</c>
/// triggers a shake in any mode.
/// </summary>
public sealed class CameraRigScene : IDemoScene
{
    private enum CameraMode
    {
        Mounted,
        Tracking,
        Free,
    }

    // Rectangular loop the cart drives around, at the cart's resting height above the floor.
    private static readonly Vector3[] LoopWaypoints =
    [
        new(-5f, 0.3f, -5f),
        new(5f, 0.3f, -5f),
        new(5f, 0.3f, 5f),
        new(-5f, 0.3f, 5f),
    ];

    // Mounted mode: behind and above the cart (local +Z is "behind" since local -Z is forward),
    // tilted slightly down so the cart stays in view instead of dead ahead.
    private static readonly Vector3 MountOffset = new(0f, 1.4f, 3.2f);
    private static readonly Quaternion MountRotation = Quaternion.CreateFromAxisAngle(
        Vector3.UnitX,
        -0.18f
    );

    private static readonly Vector3 TrackingPosition = new(0f, 9f, 13f);
    private static readonly Vector3 FreeStartPosition = new(9f, 6f, 9f);
    private static readonly Vector3 FreeStartTarget = new(0f, 0.5f, 0f);

    private const float SmoothingStep = 1f;
    private const float ShakeTrauma = 0.5f;

    private World? _world;
    private GpuMeshRegistry? _meshRegistry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private PathFollow3DSystem? _pathFollowSystem;
    private TransformHierarchySystem? _hierarchySystem;
    private CameraRigSystem? _cameraRigSystem;
    private FreeFlyCameraSystem? _freeFlySystem;

    private World? _hudWorld;
    private FontManager? _hudFontManager;
    private TextRenderer? _hudTextRenderer;
    private UnifiedRenderSystem? _hudRenderSystem;
    private Font? _hudFont;
    private Entity _modeLabel;
    private Entity _smoothingLabel;
    private Entity _traumaLabel;

    private Entity _cart;
    private Entity _camera;
    private CameraMode _mode = CameraMode.Mounted;
    private float _smoothing = 5f;

    public string Name => "Camera Rig";
    public string Description =>
        "CameraRigSystem: the Transform3D bridge, LookAtTarget, and CameraShake";
    public string Controls =>
        "1: mounted  2: tracking  3: free (WASD/Q/E, RMB-drag)  +/-: tracking smoothing  Space: shake";

    public void Load(Window window)
    {
        var world = new World();
        _world = world;
        var meshRegistry = new GpuMeshRegistry(window.Gl);
        _meshRegistry = meshRegistry;
        _textures = new TextureManager(window.Gl);

        var boxData = MeshFactory.CreateBox("camera_rig_box");
        var boxAabb = boxData.ToAabb();
        var boxMesh = meshRegistry.Register(boxData);

        BuildFloor(world, meshRegistry);
        BuildProps(world, boxMesh, boxAabb);
        _cart = BuildCart(world, boxMesh, boxAabb);

        world.AddComponent(
            world.CreateEntity("sun"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.4f)),
                Color = Color.White,
                Intensity = 1f,
            }
        );

        _camera = world.CreateEntity("camera");
        world.AddComponent(
            _camera,
            new Camera3D
            {
                Fov = MathF.PI / 4f,
                Near = 0.1f,
                Far = 200f,
            }
        );
        world.AddComponent(_camera, new CameraShake(decay: 1.5f, maxOffset: 0.25f, seed: 1));

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(
            renderer3D,
            meshRegistry,
            _textures,
            world,
            window
        );
        _pathFollowSystem = new PathFollow3DSystem(world);
        _hierarchySystem = new TransformHierarchySystem(world);
        _cameraRigSystem = new CameraRigSystem(world);
        _freeFlySystem = new FreeFlyCameraSystem(world, _camera, moveSpeed: 8f);

        SetMode(CameraMode.Mounted);

        BuildHud(window);

        Keyboard.AddKeyDown(Keys.Num1, () => SetMode(CameraMode.Mounted));
        Keyboard.AddKeyDown(Keys.Num2, () => SetMode(CameraMode.Tracking));
        Keyboard.AddKeyDown(Keys.Num3, () => SetMode(CameraMode.Free));
        Keyboard.AddKeyDown(Keys.Plus, () => AdjustSmoothing(SmoothingStep));
        Keyboard.AddKeyDown(Keys.Minus, () => AdjustSmoothing(-SmoothingStep));
        Keyboard.AddKeyDown(Keys.Space, AddShake);
    }

    // A flat 16x16 floor centred on the loop.
    private static void BuildFloor(World world, GpuMeshRegistry meshRegistry)
    {
        var floorMesh = meshRegistry.Register(
            MeshFactory.CreateQuad(
                "camera_rig_floor",
                new Vector3(-8f, 0f, 8f),
                new Vector3(8f, 0f, 8f),
                new Vector3(8f, 0f, -8f),
                new Vector3(-8f, 0f, -8f),
                Vector3.UnitY
            )
        );

        var floor = world.CreateEntity("floor");
        world.AddComponent(floor, floorMesh);
        world.AddComponent(floor, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            floor,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(50, 55, 50),
                Diffuse = new Color(110, 120, 110),
                Specular = new Color(20, 20, 20),
                Shininess = 4f,
            }
        );
    }

    // Static pillars at the loop's outside corners, purely as reference points for judging camera
    // motion — none of them move or react to anything.
    private static void BuildProps(World world, MeshHandle boxMesh, Aabb3D boxAabb)
    {
        var material = new Material3D
        {
            DiffuseTexturePath = string.Empty,
            Ambient = new Color(80, 60, 40),
            Diffuse = new Color(150, 110, 70),
            Specular = new Color(30, 30, 30),
            Shininess = 8f,
        };

        Span<Vector3> corners =
        [
            new(-7f, 1f, -7f),
            new(7f, 1f, -7f),
            new(7f, 1f, 7f),
            new(-7f, 1f, 7f),
        ];

        var index = 0;
        foreach (var corner in corners)
        {
            var pillar = world.CreateEntity($"pillar_{index++}");
            world.AddComponent(pillar, boxMesh);
            world.AddComponent(
                pillar,
                new Transform3D(corner, Quaternion.Identity, new Vector3(0.6f, 2f, 0.6f))
            );
            world.AddComponent(pillar, boxAabb);
            world.AddComponent(pillar, material);
        }
    }

    private static Entity BuildCart(World world, MeshHandle boxMesh, Aabb3D boxAabb)
    {
        var cart = world.CreateEntity("cart");
        world.AddComponent(cart, boxMesh);
        world.AddComponent(
            cart,
            new Transform3D(LoopWaypoints[0], Quaternion.Identity, new Vector3(1.2f, 0.6f, 1.8f))
        );
        world.AddComponent(cart, boxAabb);
        world.AddComponent(
            cart,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(20, 60, 90),
                Diffuse = new Color(40, 130, 200),
                Specular = new Color(80, 80, 80),
                Shininess = 32f,
            }
        );
        world.AddComponent(
            cart,
            new PathFollow3D(LoopWaypoints, speed: 3f, loopMode: TweenLoopMode.Loop)
        );

        return cart;
    }

    private void BuildHud(Window window)
    {
        var hudWorld = new World();
        _hudWorld = hudWorld;
        _hudFontManager = new FontManager();
        _hudTextRenderer = new TextRenderer(window);
        _hudRenderSystem = new UnifiedRenderSystem(null, _hudTextRenderer, hudWorld, window);
        _hudFont = _hudFontManager.Load("Assets/Shared/Roboto-Regular.ttf");

        _modeLabel = hudWorld.CreateEntity("mode_label");
        hudWorld.AddComponent(_modeLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _modeLabel,
            new Transform2D(new Vector2(-0.95f, 0.82f), scale: new Vector2(0.003f))
        );

        _smoothingLabel = hudWorld.CreateEntity("smoothing_label");
        hudWorld.AddComponent(_smoothingLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _smoothingLabel,
            new Transform2D(new Vector2(-0.95f, 0.74f), scale: new Vector2(0.003f))
        );

        _traumaLabel = hudWorld.CreateEntity("trauma_label");
        hudWorld.AddComponent(_traumaLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _traumaLabel,
            new Transform2D(new Vector2(-0.95f, 0.66f), scale: new Vector2(0.003f))
        );

        UpdateHud();
    }

    // Tears down whichever of Parent/LocalTransform3D/Transform3D/LookAtTarget the previous mode
    // left on the camera before wiring up the new one, so switching modes repeatedly never leaves
    // a stale bridge or look-at fighting the newly selected mode.
    private void SetMode(CameraMode mode)
    {
        if (_world is not { } world)
            return;

        _mode = mode;

        world.RemoveComponent<Parent>(_camera);
        world.RemoveComponent<LocalTransform3D>(_camera);
        world.RemoveComponent<Transform3D>(_camera);
        world.RemoveComponent<LookAtTarget>(_camera);

        var camera = world.GetComponent<Camera3D>(_camera);

        switch (mode)
        {
            case CameraMode.Mounted:
                world.AddComponent(_camera, new Parent(_cart));
                world.AddComponent(
                    _camera,
                    new LocalTransform3D(MountOffset, MountRotation, Vector3.One)
                );
                break;

            case CameraMode.Tracking:
                world.AddComponent(
                    _camera,
                    camera with
                    {
                        Position = TrackingPosition,
                        Up = Vector3.UnitY,
                    }
                );
                world.AddComponent(_camera, new LookAtTarget(_cart, _smoothing));
                break;

            case CameraMode.Free:
                world.AddComponent(
                    _camera,
                    camera with
                    {
                        Position = FreeStartPosition,
                        Target = FreeStartTarget,
                        Up = Vector3.UnitY,
                    }
                );
                break;
        }

        UpdateHud();
    }

    private void AdjustSmoothing(float delta)
    {
        _smoothing = Math.Clamp(_smoothing + delta, -2f, 20f);

        if (
            _mode == CameraMode.Tracking
            && _world is { } world
            && world.TryGetComponent<LookAtTarget>(_camera, out var lookAt)
        )
        {
            lookAt.Smoothing = _smoothing;
            world.AddComponent(_camera, lookAt);
        }

        UpdateHud();
    }

    private void AddShake()
    {
        if (_world is not { } world || !world.TryGetComponent<CameraShake>(_camera, out var shake))
            return;

        world.AddComponent(_camera, shake.AddTrauma(ShakeTrauma));
    }

    private void UpdateHud()
    {
        if (_hudWorld is not { } hudWorld || _hudFont is not { } font)
            return;

        var modeText = _mode switch
        {
            CameraMode.Mounted => "Mounted (rides the cart via Parent + LocalTransform3D)",
            CameraMode.Tracking => "Tracking (fixed position, LookAtTarget on the cart)",
            _ => "Free (FreeFlyCameraSystem)",
        };
        hudWorld.AddComponent(_modeLabel, new Text($"Mode: {modeText}", font, 18, Color.White));

        var smoothingText =
            _mode == CameraMode.Tracking
                ? $"Smoothing: {_smoothing:0.00} (+/- to adjust, <= 0 snaps)"
                : $"Smoothing: {_smoothing:0.00} (tracking mode only)";
        hudWorld.AddComponent(_smoothingLabel, new Text(smoothingText, font, 18, Color.White));

        var trauma =
            _world is { } world && world.TryGetComponent<CameraShake>(_camera, out var shake)
                ? shake.Trauma
                : 0f;
        hudWorld.AddComponent(
            _traumaLabel,
            new Text($"Trauma: {trauma:0.00} (Space to shake)", font, 18, Color.White)
        );
    }

    public void Update(float deltaTime)
    {
        // Run order (docs/camera.md "Update order"): whatever positions the camera runs first —
        // the cart's own path-follow movement, then TransformHierarchySystem resolving the
        // Mounted mode's Parent + LocalTransform3D bridge (a no-op in Tracking/Free, since the
        // camera carries no Parent there) and FreeFlyCameraSystem in Free mode — and
        // CameraRigSystem runs last, right before render, so its Transform3D bridge/LookAtTarget/
        // CameraShake steps always see this frame's final positions rather than last frame's.
        _pathFollowSystem?.Update(deltaTime);
        _hierarchySystem?.Update(deltaTime);

        if (_mode == CameraMode.Free)
            _freeFlySystem?.Update(deltaTime);

        _cameraRigSystem?.Update(deltaTime);

        UpdateHud();
    }

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _hudRenderSystem?.Render();
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.Num1);
        Keyboard.RemoveKeyDown(Keys.Num2);
        Keyboard.RemoveKeyDown(Keys.Num3);
        Keyboard.RemoveKeyDown(Keys.Plus);
        Keyboard.RemoveKeyDown(Keys.Minus);
        Keyboard.RemoveKeyDown(Keys.Space);

        _hudTextRenderer?.Dispose();
        _hudFontManager?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _meshRegistry?.Dispose();
    }
}

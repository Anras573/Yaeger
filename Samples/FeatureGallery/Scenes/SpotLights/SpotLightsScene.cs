using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.SpotLights;

/// <summary>
/// Spot Lights demo (issue #274): <see cref="SpotLight"/> isn't used by any other sample. A dim
/// room — floor, back wall, a handful of boxes/pillars — lit only by a weak directional light, so
/// three coloured spot lights sweeping back and forth (their <see cref="SpotLight.Direction"/>
/// rotated every frame, not driven by <see cref="Transform3D"/> rotation — <c>MeshRenderSystem</c>
/// reads the field directly) dominate the lighting and show off cone-edge falloff and range. A
/// fourth, white spot light acts as a flashlight that follows <see cref="FreeFlyCameraSystem"/>'s
/// camera every frame; <c>F</c> toggles it. <c>[</c>/<c>]</c> scale every spot's cone angles at
/// once, and <c>P</c> flips <see cref="Material3D.UsePbr"/> on the room's materials to show spot
/// lights work in both shading paths (see docs/pbr.md).
/// </summary>
public sealed class SpotLightsScene : IDemoScene
{
    private const float MinConeScale = 0.5f;
    private const float MaxConeScale = 2f;
    private const float ConeScaleStep = 0.15f;

    private const float SpotBaseInnerConeAngle = MathF.PI / 15f; // 12°
    private const float SpotBaseOuterConeAngle = MathF.PI / 8.18f; // ~22°

    private readonly record struct SweepingLight(
        Entity Entity,
        Vector3 BaseDirection,
        float Amplitude,
        float Speed,
        float Phase,
        float BaseInnerConeAngle,
        float BaseOuterConeAngle,
        Color Color,
        float Intensity,
        float Range
    );

    private const float FlashlightBaseInnerConeAngle = MathF.PI / 18f; // 10°
    private const float FlashlightBaseOuterConeAngle = MathF.PI / 11.25f; // 16°
    private const float FlashlightIntensity = 8f;
    private const float FlashlightRange = 14f;

    private readonly List<SweepingLight> _sweepingLights = [];
    private readonly List<(Entity Entity, Material3D BaseMaterial)> _materials = [];

    private World? _world;
    private GpuMeshRegistry? _registry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private FreeFlyCameraSystem? _freeFlySystem;
    private Entity _cameraEntity;
    private Entity _flashlightEntity;

    private World? _hudWorld;
    private FontManager? _hudFontManager;
    private TextRenderer? _hudTextRenderer;
    private UnifiedRenderSystem? _hudRenderSystem;
    private Font? _hudFont;
    private Entity _coneLabel;
    private Entity _flashlightLabel;
    private Entity _shadingLabel;

    private float _elapsed;
    private float _coneScale = 1f;
    private bool _flashlightOn;
    private bool _usePbr;

    public string Name => "Spot Lights";
    public string Description =>
        "Sweeping coloured SpotLights, a camera-following flashlight, cone/PBR toggles";
    public string Controls =>
        "WASD move, Q/E up/down, RMB-drag look, F: flashlight, [/]: cone angle, P: toggle PBR";

    public void Load(Window window)
    {
        var world = new World();
        _world = world;
        var registry = new GpuMeshRegistry(window.Gl);
        _registry = registry;
        _textures = new TextureManager(window.Gl);

        BuildRoom(world, registry);
        BuildSweepingLights(world);

        // Weak directional light only — the room should read as dark except where the spots land.
        world.AddComponent(
            world.CreateEntity("sun"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.2f, 1f, 0.3f)),
                Color = Color.White,
                Intensity = 0.12f,
            }
        );

        var cameraEntity = world.CreateEntity("camera");
        _cameraEntity = cameraEntity;
        world.AddComponent(
            cameraEntity,
            new Camera3D(
                Position: new Vector3(0f, 2.2f, 9f),
                Target: new Vector3(0f, 1.6f, -1f),
                Up: Vector3.UnitY,
                Fov: MathF.PI / 4f,
                Near: 0.1f,
                Far: 200f
            )
        );

        _flashlightEntity = world.CreateEntity("flashlight");
        world.AddComponent(_flashlightEntity, Transform3D.Identity);

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(renderer3D, registry, _textures, world, window);
        _freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 6f);

        BuildHud(window);

        Keyboard.AddKeyDown(Keys.F, ToggleFlashlight);
        Keyboard.AddKeyDown(Keys.LeftBracket, () => AdjustConeScale(-ConeScaleStep));
        Keyboard.AddKeyDown(Keys.RightBracket, () => AdjustConeScale(ConeScaleStep));
        Keyboard.AddKeyDown(Keys.P, TogglePbr);
    }

    private void BuildRoom(World world, GpuMeshRegistry registry)
    {
        var boxMeshData = MeshFactory.CreateBox("box");
        var boxHandle = registry.Register(boxMeshData);
        var boxAabb = boxMeshData.ToAabb();

        void AddSurface(string tag, MeshData meshData, Vector3 position, Material3D material)
        {
            var entity = world.CreateEntity(tag);
            world.AddComponent(entity, registry.Register(meshData));
            world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, Vector3.One));
            world.AddComponent(entity, material);
            world.AddComponent(entity, meshData.ToAabb());
            _materials.Add((entity, material));
        }

        // All pillars share the one registered box mesh (and, since they also share boxMaterial,
        // the same Material3D) so MeshRenderSystem's (MeshHandle, Material3D) instancing groups
        // them into a single glDrawElementsInstanced call once the group reaches InstancingThreshold.
        void AddBox(string tag, Vector3 position, Vector3 scale, Material3D material)
        {
            var entity = world.CreateEntity(tag);
            world.AddComponent(entity, boxHandle);
            world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, scale));
            world.AddComponent(entity, material);
            world.AddComponent(entity, boxAabb);
            _materials.Add((entity, material));
        }

        static Material3D Matte(Color diffuse) =>
            new()
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(
                    (byte)(diffuse.R * 0.2f),
                    (byte)(diffuse.G * 0.2f),
                    (byte)(diffuse.B * 0.2f)
                ),
                Diffuse = diffuse,
                Specular = new Color(20, 20, 20),
                Shininess = 8f,
                MetallicFactor = 0f,
                RoughnessFactor = 0.9f,
            };

        var floorMaterial = Matte(new Color(170, 170, 180));
        var wallMaterial = Matte(new Color(120, 120, 130));
        var boxMaterial = Matte(new Color(150, 145, 140));

        const float halfWidth = 12f;
        const float halfDepth = 11f;
        const float wallHeight = 8f;
        const float backZ = -halfDepth;
        const float frontZ = 3f;

        // Floor (y = 0, normal +Y).
        AddSurface(
            "floor",
            MeshFactory.CreateQuad(
                "floor",
                new Vector3(-halfWidth, 0f, frontZ),
                new Vector3(halfWidth, 0f, frontZ),
                new Vector3(halfWidth, 0f, backZ),
                new Vector3(-halfWidth, 0f, backZ),
                Vector3.UnitY
            ),
            Vector3.Zero,
            floorMaterial
        );

        // Back wall (z = backZ, normal +Z).
        AddSurface(
            "back_wall",
            MeshFactory.CreateQuad(
                "back_wall",
                new Vector3(-halfWidth, 0f, backZ),
                new Vector3(halfWidth, 0f, backZ),
                new Vector3(halfWidth, wallHeight, backZ),
                new Vector3(-halfWidth, wallHeight, backZ),
                Vector3.UnitZ
            ),
            Vector3.Zero,
            wallMaterial
        );

        // Boxes/pillars of varying height for the sweeping cones and the flashlight to fall on.
        AddBox("pillar_1", new Vector3(-6f, 1.5f, -6f), new Vector3(1.6f, 3f, 1.6f), boxMaterial);
        AddBox("pillar_2", new Vector3(-2f, 2.5f, -8.5f), new Vector3(1.6f, 5f, 1.6f), boxMaterial);
        AddBox("pillar_3", new Vector3(2.5f, 1f, -5f), new Vector3(1.6f, 2f, 1.6f), boxMaterial);
        AddBox("pillar_4", new Vector3(6f, 2f, -7f), new Vector3(1.6f, 4f, 1.6f), boxMaterial);
        AddBox("pillar_5", new Vector3(0f, 3f, -9.5f), new Vector3(1.8f, 6f, 1.8f), boxMaterial);
    }

    private void BuildSweepingLights(World world)
    {
        void AddSweepingLight(
            string tag,
            Vector3 position,
            Color color,
            float speed,
            float phase,
            float amplitude
        )
        {
            var entity = world.CreateEntity(tag);
            var baseDirection = Vector3.Normalize(new Vector3(0f, -1f, 0.55f));
            world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, Vector3.One));

            world.AddComponent(
                entity,
                new SpotLight
                {
                    Color = color,
                    Intensity = 7f,
                    Direction = baseDirection,
                    InnerConeAngle = SpotBaseInnerConeAngle,
                    OuterConeAngle = SpotBaseOuterConeAngle,
                    Range = 15f,
                }
            );

            _sweepingLights.Add(
                new SweepingLight(
                    entity,
                    baseDirection,
                    amplitude,
                    speed,
                    phase,
                    SpotBaseInnerConeAngle,
                    SpotBaseOuterConeAngle,
                    color,
                    7f,
                    15f
                )
            );
        }

        AddSweepingLight(
            "spot_red",
            new Vector3(-5f, 6f, -9f),
            new Color(255, 60, 60),
            speed: 0.5f,
            phase: 0f,
            amplitude: 0.9f
        );
        AddSweepingLight(
            "spot_green",
            new Vector3(0f, 6.5f, -9f),
            new Color(70, 255, 120),
            speed: 0.7f,
            phase: 2.1f,
            amplitude: 0.7f
        );
        AddSweepingLight(
            "spot_blue",
            new Vector3(5f, 6f, -9f),
            new Color(80, 160, 255),
            speed: 0.35f,
            phase: 4.2f,
            amplitude: 1.1f
        );
    }

    private void BuildHud(Window window)
    {
        var hudWorld = new World();
        _hudWorld = hudWorld;
        _hudFontManager = new FontManager();
        _hudTextRenderer = new TextRenderer(window);
        _hudRenderSystem = new UnifiedRenderSystem(null, _hudTextRenderer, hudWorld, window);
        _hudFont = _hudFontManager.Load("Assets/Shared/Roboto-Regular.ttf");

        _coneLabel = hudWorld.CreateEntity("cone_label");
        hudWorld.AddComponent(_coneLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _coneLabel,
            new Transform2D(new Vector2(-0.95f, 0.82f), scale: new Vector2(0.003f))
        );

        _flashlightLabel = hudWorld.CreateEntity("flashlight_label");
        hudWorld.AddComponent(_flashlightLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _flashlightLabel,
            new Transform2D(new Vector2(-0.95f, 0.74f), scale: new Vector2(0.003f))
        );

        _shadingLabel = hudWorld.CreateEntity("shading_label");
        hudWorld.AddComponent(_shadingLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _shadingLabel,
            new Transform2D(new Vector2(-0.95f, 0.66f), scale: new Vector2(0.003f))
        );

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_hudWorld is not { } hudWorld || _hudFont is not { } font)
            return;

        var innerDeg = SpotBaseInnerConeAngle * _coneScale * (180f / MathF.PI);
        var outerDeg = SpotBaseOuterConeAngle * _coneScale * (180f / MathF.PI);

        hudWorld.AddComponent(
            _coneLabel,
            new Text(
                $"Spot cone: {innerDeg:0}° / {outerDeg:0}°  ([ narrower, ] wider)",
                font,
                18,
                Color.White
            )
        );
        hudWorld.AddComponent(
            _flashlightLabel,
            new Text($"Flashlight: {(_flashlightOn ? "On" : "Off")}  (F)", font, 18, Color.White)
        );
        hudWorld.AddComponent(
            _shadingLabel,
            new Text($"Shading: {(_usePbr ? "PBR" : "Blinn-Phong")}  (P)", font, 18, Color.White)
        );
    }

    private void ToggleFlashlight()
    {
        if (_world is not { } world)
            return;

        _flashlightOn = !_flashlightOn;
        if (!_flashlightOn)
            world.RemoveComponent<SpotLight>(_flashlightEntity);
        UpdateHud();
    }

    private void AdjustConeScale(float delta)
    {
        _coneScale = Math.Clamp(_coneScale + delta, MinConeScale, MaxConeScale);
        UpdateHud();
    }

    private void TogglePbr()
    {
        if (_world is not { } world)
            return;

        _usePbr = !_usePbr;
        foreach (var (entity, baseMaterial) in _materials)
            world.AddComponent(entity, baseMaterial with { UsePbr = _usePbr });
        UpdateHud();
    }

    public void Update(float deltaTime)
    {
        _freeFlySystem?.Update(deltaTime);

        if (_world is not { } world)
            return;

        _elapsed += deltaTime;

        foreach (var light in _sweepingLights)
        {
            var angle = light.Amplitude * MathF.Sin(_elapsed * light.Speed + light.Phase);
            var direction = Vector3.Normalize(
                Vector3.TransformNormal(
                    light.BaseDirection,
                    Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, angle)
                )
            );

            world.AddComponent(
                light.Entity,
                new SpotLight
                {
                    Color = light.Color,
                    Intensity = light.Intensity,
                    Direction = direction,
                    InnerConeAngle = light.BaseInnerConeAngle * _coneScale,
                    OuterConeAngle = light.BaseOuterConeAngle * _coneScale,
                    Range = light.Range,
                }
            );
        }

        if (world.TryGetComponent<Camera3D>(_cameraEntity, out var camera))
        {
            var forward = Vector3.Normalize(camera.Target - camera.Position);
            world.AddComponent(
                _flashlightEntity,
                new Transform3D(camera.Position, Quaternion.Identity, Vector3.One)
            );

            if (_flashlightOn)
            {
                world.AddComponent(
                    _flashlightEntity,
                    new SpotLight
                    {
                        Color = Color.White,
                        Intensity = FlashlightIntensity,
                        Direction = forward,
                        InnerConeAngle = FlashlightBaseInnerConeAngle * _coneScale,
                        OuterConeAngle = FlashlightBaseOuterConeAngle * _coneScale,
                        Range = FlashlightRange,
                    }
                );
            }
        }
    }

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _hudRenderSystem?.Render();
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.F);
        Keyboard.RemoveKeyDown(Keys.LeftBracket);
        Keyboard.RemoveKeyDown(Keys.RightBracket);
        Keyboard.RemoveKeyDown(Keys.P);

        _hudTextRenderer?.Dispose();
        _hudFontManager?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _registry?.Dispose();
    }
}

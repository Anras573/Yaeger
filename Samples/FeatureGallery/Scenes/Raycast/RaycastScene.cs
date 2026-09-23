using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Physics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Raycast;

/// <summary>
/// Raycast demo (issue #272): <see cref="WorldRaycastExtensions.Raycast"/>/<c>RaycastAll</c>
/// against a field of boxes spread across two layers. LMB fires a single <c>Raycast</c> — it
/// tints the nearest hit box and drops a small marker at <see cref="RaycastHit.Point"/> oriented
/// along <see cref="RaycastHit.Normal"/>; Shift+LMB fires <c>RaycastAll</c> — it tints every box
/// the ray passes through instead of just the nearest one. <c>M</c> cycles the layer mask. A
/// three-box "depth stack" sits directly behind the centre of the field so a ray aimed straight
/// down it (the camera's default view) demonstrates <c>RaycastAll</c> returning multiple hits
/// without having to hunt for an angle where boxes line up. See docs/queries.md.
/// </summary>
public sealed class RaycastScene : IDemoScene
{
    private enum MaskMode
    {
        All,
        Layer0Only,
        Layer1Only,
    }

    private const uint Layer0Bit = 1u << 0;
    private const uint Layer1Bit = 1u << 1;
    private const float MaxRayDistance = 200f;
    private static readonly Vector3 MarkerScale = new(0.35f, 0.04f, 0.35f);

    private static readonly Material3D HighlightMaterial = new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color(120, 100, 10),
        Diffuse = new Color(255, 215, 0),
        Specular = new Color(80, 80, 80),
        Shininess = 32f,
    };

    private static readonly Material3D MarkerMaterial = new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color(20, 120, 160),
        Diffuse = new Color(40, 200, 255),
        Specular = Color.White,
        Shininess = 48f,
    };

    private readonly Dictionary<Entity, Material3D> _originalMaterials = new();
    private readonly Dictionary<Entity, string> _entityNames = new();
    private readonly List<Entity> _tintedEntities = [];

    private Window? _window;
    private World? _world;
    private GpuMeshRegistry? _registry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private FreeFlyCameraSystem? _freeFlySystem;
    private Entity _cameraEntity;

    private World? _hudWorld;
    private FontManager? _hudFontManager;
    private TextRenderer? _hudTextRenderer;
    private UnifiedRenderSystem? _hudRenderSystem;
    private Font? _hudFont;
    private Entity _maskLabel;
    private Entity _resultLabel;

    private MeshHandle _markerMesh;
    private Entity _marker;
    private bool _markerVisible;

    private MaskMode _mask = MaskMode.All;

    public string Name => "Raycast";
    public string Description =>
        "World.Raycast / RaycastAll against a field of boxes, with layer masks";
    public string Controls =>
        "WASD/Q/E move, RMB-drag look, LMB: raycast, Shift+LMB: raycast all, M: cycle mask";

    public void Load(Window window)
    {
        _window = window;

        var world = new World();
        _world = world;
        var registry = new GpuMeshRegistry(window.Gl);
        _registry = registry;
        _textures = new TextureManager(window.Gl);

        var boxData = MeshFactory.CreateBox("raycast_box");
        var boxAabb = boxData.ToAabb();
        var boxMesh = registry.Register(boxData);
        _markerMesh = boxMesh;

        BuildField(world, boxMesh, boxAabb);

        _marker = world.CreateEntity("hit_marker");
        world.AddComponent(
            _marker,
            new Transform3D(Vector3.Zero, Quaternion.Identity, MarkerScale)
        );

        world.AddComponent(
            world.CreateEntity("sun"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.5f)),
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var cameraEntity = world.CreateEntity("camera");
        _cameraEntity = cameraEntity;
        world.AddComponent(
            cameraEntity,
            new Camera3D(
                Position: new Vector3(0f, 3.2f, 13f),
                Target: new Vector3(0f, 3.2f, 0f),
                Up: Vector3.UnitY,
                Fov: MathF.PI / 4f,
                Near: 0.1f,
                Far: 200f
            )
        );

        var renderer3D = new Renderer3D(window.Gl);
        _renderer3D = renderer3D;
        _meshRenderSystem = new MeshRenderSystem(renderer3D, registry, _textures, world, window);
        _freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 5f);

        // A separate 2D world+TextRenderer just for the HUD: MeshRenderSystem only knows how to
        // draw MeshHandle/Material3D entities, so the mask/result readout is its own tiny
        // screen-space scene rendered after the 3D pass, same shape as SceneHost's own HUD bar.
        var hudWorld = new World();
        _hudWorld = hudWorld;
        _hudFontManager = new FontManager();
        _hudTextRenderer = new TextRenderer(window);
        _hudRenderSystem = new UnifiedRenderSystem(null, _hudTextRenderer, hudWorld, window);
        _hudFont = _hudFontManager.Load("Assets/Shared/Roboto-Regular.ttf");

        _maskLabel = hudWorld.CreateEntity("mask_label");
        hudWorld.AddComponent(_maskLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _maskLabel,
            new Transform2D(new Vector2(-0.95f, 0.82f), scale: new Vector2(0.003f))
        );

        _resultLabel = hudWorld.CreateEntity("result_label");
        hudWorld.AddComponent(_resultLabel, new Text("", _hudFont, 18, Color.White));
        hudWorld.AddComponent(
            _resultLabel,
            new Transform2D(new Vector2(-0.95f, 0.74f), scale: new Vector2(0.003f))
        );

        UpdateMaskLabel();
        SetResult("Aim at the field and click.");

        Yaeger.Input.Mouse.AddButtonDown(MouseButton.Left, OnLeftMouseDown);
        Keyboard.AddKeyDown(Keys.M, CycleMask);
    }

    // A grid of boxes across two layers (checkerboard grey/red), with a few boxes rotated or
    // non-uniformly scaled so the oriented-box slab test is visibly exercised, plus a two-box
    // "depth stack" directly behind the grid's centre so RaycastAll has multiple hits to report
    // without requiring the player to hunt for an angle where boxes line up.
    private void BuildField(World world, MeshHandle boxMesh, Aabb3D boxAabb)
    {
        var grey = new Material3D
        {
            DiffuseTexturePath = string.Empty,
            Ambient = new Color(60, 60, 65),
            Diffuse = new Color(150, 150, 160),
            Specular = new Color(40, 40, 40),
            Shininess = 16f,
        };
        var red = new Material3D
        {
            DiffuseTexturePath = string.Empty,
            Ambient = new Color(90, 15, 10),
            Diffuse = new Color(200, 40, 30),
            Specular = new Color(60, 60, 60),
            Shininess = 16f,
        };

        void AddBox(
            string name,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            int layer,
            Material3D material
        )
        {
            var entity = world.CreateEntity(name);
            world.AddComponent(entity, boxMesh);
            world.AddComponent(entity, new Transform3D(position, rotation, scale));
            world.AddComponent(entity, boxAabb with { Layer = layer });
            world.AddComponent(entity, material);
            _originalMaterials[entity] = material;
            _entityNames[entity] = name;
        }

        const int columns = 5;
        const int rows = 3;
        const float spacingX = 2.2f;
        const float spacingY = 2.2f;
        var originX = -(columns - 1) * spacingX * 0.5f;
        const float originY = 1f;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var index = row * columns + col;
                var layer = (row + col) % 2 == 0 ? 1 : 0;
                var material = layer == 1 ? red : grey;
                var position = new Vector3(originX + col * spacingX, originY + row * spacingY, 0f);
                var rotation =
                    index % 3 == 0
                        ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 6f)
                        : Quaternion.Identity;
                var scale = index % 5 == 0 ? new Vector3(1.6f, 0.7f, 1f) : Vector3.One;

                AddBox($"box_{row}_{col}", position, rotation, scale, layer, material);
            }
        }

        var centerX = originX + columns / 2 * spacingX;
        var centerY = originY + rows / 2 * spacingY;
        AddBox(
            "depth_1",
            new Vector3(centerX, centerY, -4f),
            Quaternion.Identity,
            Vector3.One,
            0,
            grey
        );
        AddBox(
            "depth_2",
            new Vector3(centerX, centerY, -8f),
            Quaternion.Identity,
            Vector3.One,
            1,
            red
        );
    }

    private void OnLeftMouseDown()
    {
        if (
            _world is not { } world
            || _window is not { } window
            || !world.TryGetComponent<Camera3D>(_cameraEntity, out var camera)
        )
            return;

        var size = window.Size;
        var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
        var viewProj = camera.ViewProjection(aspectRatio);

        if (
            !ViewportPicking.TryGetPickRay(
                Yaeger.Input.Mouse.PositionNdc,
                viewProj,
                out var origin,
                out var direction
            )
        )
            return;

        var ray = new Ray3D(origin, direction);
        var mask = MaskBits();

        ClearTints();

        if (Keyboard.IsKeyPressed(Keys.Shift))
        {
            var hits = world.RaycastAll(ray, MaxRayDistance, mask);
            HideMarker();
            foreach (var hit in hits)
                Tint(hit.Entity);

            SetResult(
                hits.Count == 0
                    ? "RaycastAll: no hits."
                    : $"RaycastAll: {hits.Count} hit(s) at "
                        + string.Join(", ", hits.Select(h => h.Distance.ToString("0.00")))
            );
        }
        else
        {
            var hit = world.Raycast(ray, MaxRayDistance, mask);
            if (hit is { } h)
            {
                Tint(h.Entity);
                ShowMarker(h.Point, h.Normal);
                SetResult($"Raycast: hit {EntityLabel(h.Entity)} at {h.Distance:0.00}");
            }
            else
            {
                HideMarker();
                SetResult("Raycast: no hit.");
            }
        }
    }

    private string EntityLabel(Entity entity) =>
        _entityNames.TryGetValue(entity, out var name) ? name : $"entity {entity.Id}";

    private uint MaskBits() =>
        _mask switch
        {
            MaskMode.Layer0Only => Layer0Bit,
            MaskMode.Layer1Only => Layer1Bit,
            _ => WorldRaycastExtensions.AllLayers,
        };

    private static string MaskDescription(MaskMode mode) =>
        mode switch
        {
            MaskMode.Layer0Only => "Layer 0 only (grey)",
            MaskMode.Layer1Only => "Layer 1 only (red)",
            _ => "All layers",
        };

    private void CycleMask()
    {
        _mask = _mask switch
        {
            MaskMode.All => MaskMode.Layer0Only,
            MaskMode.Layer0Only => MaskMode.Layer1Only,
            _ => MaskMode.All,
        };
        UpdateMaskLabel();
    }

    private void UpdateMaskLabel() =>
        SetLabel(_maskLabel, $"Mask: {MaskDescription(_mask)}  (M to cycle)");

    private void SetResult(string message) => SetLabel(_resultLabel, message);

    private void SetLabel(Entity label, string content)
    {
        if (_hudWorld is not { } hudWorld || _hudFont is not { } font)
            return;
        hudWorld.AddComponent(label, new Text(content, font, 18, Color.White));
    }

    private void Tint(Entity entity)
    {
        if (_world is not { } world || !_originalMaterials.ContainsKey(entity))
            return;
        world.AddComponent(entity, HighlightMaterial);
        _tintedEntities.Add(entity);
    }

    private void ClearTints()
    {
        if (_world is not { } world)
            return;
        foreach (var entity in _tintedEntities)
        {
            if (_originalMaterials.TryGetValue(entity, out var material))
                world.AddComponent(entity, material);
        }
        _tintedEntities.Clear();
    }

    private void ShowMarker(Vector3 point, Vector3 normal)
    {
        if (_world is not { } world)
            return;

        var rotation = RotationFromTo(Vector3.UnitY, normal);
        // Nudge the marker off the surface along its normal to avoid z-fighting with the box
        // face it sits on.
        var position = point + normal * 0.01f;
        world.AddComponent(_marker, new Transform3D(position, rotation, MarkerScale));

        if (!_markerVisible)
        {
            world.AddComponent(_marker, _markerMesh);
            world.AddComponent(_marker, MarkerMaterial);
            _markerVisible = true;
        }
    }

    private void HideMarker()
    {
        if (_world is not { } world || !_markerVisible)
            return;
        world.RemoveComponent<MeshHandle>(_marker);
        _markerVisible = false;
    }

    // Shortest-arc rotation taking unit vector `from` onto unit vector `to`, used to orient the
    // flat marker's local up axis (MarkerScale is thin along Y) along a hit's surface normal.
    private static Quaternion RotationFromTo(Vector3 from, Vector3 to)
    {
        var dot = Vector3.Dot(from, to);

        if (dot > 0.9999f)
            return Quaternion.Identity;

        if (dot < -0.9999f)
        {
            var axis = Vector3.Cross(Vector3.UnitX, from);
            if (axis.LengthSquared() < 1e-6f)
                axis = Vector3.Cross(Vector3.UnitZ, from);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }

        var cross = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(cross, 1f + dot));
    }

    public void Update(float deltaTime) => _freeFlySystem?.Update(deltaTime);

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _hudRenderSystem?.Render();
    }

    public void Dispose()
    {
        Yaeger.Input.Mouse.RemoveButtonDown(MouseButton.Left);
        Keyboard.RemoveKeyDown(Keys.M);

        _hudTextRenderer?.Dispose();
        _hudFontManager?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _registry?.Dispose();
    }
}

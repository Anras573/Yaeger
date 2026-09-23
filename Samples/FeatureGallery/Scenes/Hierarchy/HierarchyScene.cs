using System.Numerics;
using FeatureGallery.Shared;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Hierarchy;

/// <summary>
/// Hierarchy demo (issue #271): a three-level "orrery" — a sun rotating in place, a planet
/// parented to it via <see cref="Parent"/> + <see cref="LocalTransform3D"/> at a fixed local
/// offset, and a moon parented to the planet the same way. The scene only ever writes the sun's
/// own <see cref="Transform3D"/> rotation and the planet/moon's <see cref="LocalTransform3D"/>
/// rotation — <see cref="TransformHierarchySystem"/> alone turns those local spins into the
/// planet orbiting the sun and the moon orbiting the planet. There is no orbit math here.
/// </summary>
public sealed class HierarchyScene : IDemoScene
{
    private enum Body
    {
        Sun,
        Planet,
        Moon,
    }

    private const float PlanetOrbitRadius = 2.2f;
    private const float MoonOrbitRadius = 0.7f;
    private const float SpeedStep = 0.25f;

    private World? _world;
    private GpuMeshRegistry? _meshRegistry;
    private TextureManager? _textures;
    private Renderer3D? _renderer3D;
    private MeshRenderSystem? _meshRenderSystem;
    private TransformHierarchySystem? _hierarchySystem;
    private FreeFlyCameraSystem? _freeFlySystem;
    private ImGuiInspector? _inspector;

    private MeshHandle _sunMesh;
    private MeshHandle _planetMesh;
    private MeshHandle _moonMesh;
    private Aabb3D _boxAabb;

    private Entity _sun;
    private Entity _planet;
    private Entity _moon;
    private bool _bodiesBuilt;
    private bool _planetAlive;

    // Indexed by Body.
    private readonly float[] _speeds = new float[3];
    private Body _selected = Body.Sun;

    public string Name => "Hierarchy";
    public string Description => "Parent + LocalTransform3D composing a three-level orrery";
    public string Controls =>
        "1/2/3: select sun/planet/moon  +/-: adjust selected speed  O: destroy planet  R: rebuild  F1: inspector";

    public void Load(Window window)
    {
        var world = new World();
        _world = world;
        var meshRegistry = new GpuMeshRegistry(window.Gl);
        _meshRegistry = meshRegistry;
        _textures = new TextureManager(window.Gl);

        var boxData = MeshFactory.CreateBox("orrery_box");
        _boxAabb = boxData.ToAabb();
        var boxMesh = meshRegistry.Register(boxData);
        _sunMesh = boxMesh;
        _planetMesh = boxMesh;
        _moonMesh = boxMesh;

        BuildBodies(world);

        world.AddComponent(
            world.CreateEntity("sunlight"),
            new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.5f)),
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var cameraEntity = world.CreateEntity("camera");
        world.AddComponent(
            cameraEntity,
            new Camera3D(
                Position: new Vector3(0f, 4f, 7f),
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
        _hierarchySystem = new TransformHierarchySystem(world);
        _freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 4f);

        var inspector = new ImGuiInspector(window, world);
        _inspector = inspector;

        Keyboard.AddKeyDown(Keys.Num1, () => Select(Body.Sun));
        Keyboard.AddKeyDown(Keys.Num2, () => Select(Body.Planet));
        Keyboard.AddKeyDown(Keys.Num3, () => Select(Body.Moon));
        Keyboard.AddKeyDown(Keys.Plus, () => AdjustSpeed(SpeedStep));
        Keyboard.AddKeyDown(Keys.Minus, () => AdjustSpeed(-SpeedStep));
        Keyboard.AddKeyDown(Keys.O, DestroyPlanet);
        Keyboard.AddKeyDown(Keys.R, () => BuildBodies(world));

        Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
    }

    // (Re)creates the sun, planet, and moon from scratch, tearing down any previous instances
    // first — this is what both Load and the R ("rebuild") key use, so orphaning the moon with O
    // and then pressing R restores the full three-level hierarchy exactly as it started.
    private void BuildBodies(World world)
    {
        if (_bodiesBuilt)
        {
            // The moon may already be orphaned (Parent removed by TransformHierarchySystem after
            // O destroyed the planet) or still parented — either way it is still a live entity
            // and destroying it directly (rather than DestroyHierarchy) works for both cases.
            world.DestroyEntity(_moon);
            if (_planetAlive)
                world.DestroyEntity(_planet);
            world.DestroyEntity(_sun);
        }

        _speeds[(int)Body.Sun] = 0.4f;
        _speeds[(int)Body.Planet] = 1.0f;
        _speeds[(int)Body.Moon] = 2.2f;
        _selected = Body.Sun;

        _sun = world.CreateEntity("sun");
        world.AddComponent(_sun, _sunMesh);
        world.AddComponent(
            _sun,
            new Transform3D(Vector3.Zero, Quaternion.Identity, new Vector3(1f))
        );
        world.AddComponent(_sun, _boxAabb);
        world.AddComponent(
            _sun,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(180, 120, 20),
                Diffuse = new Color(255, 170, 30),
                Specular = new Color(40, 40, 40),
                Shininess = 8f,
            }
        );

        _planet = world.CreateEntity("planet");
        world.AddComponent(_planet, _planetMesh);
        world.AddComponent(_planet, new Parent(_sun));
        world.AddComponent(
            _planet,
            new LocalTransform3D(
                new Vector3(PlanetOrbitRadius, 0f, 0f),
                Quaternion.Identity,
                new Vector3(0.4f)
            )
        );
        world.AddComponent(_planet, _boxAabb);
        world.AddComponent(
            _planet,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(20, 50, 110),
                Diffuse = new Color(50, 110, 220),
                Specular = new Color(80, 80, 80),
                Shininess = 24f,
            }
        );
        _planetAlive = true;

        _moon = world.CreateEntity("moon");
        world.AddComponent(_moon, _moonMesh);
        world.AddComponent(_moon, new Parent(_planet));
        world.AddComponent(
            _moon,
            new LocalTransform3D(
                new Vector3(MoonOrbitRadius, 0f, 0f),
                Quaternion.Identity,
                new Vector3(0.15f)
            )
        );
        world.AddComponent(_moon, _boxAabb);
        world.AddComponent(
            _moon,
            new Material3D
            {
                DiffuseTexturePath = string.Empty,
                Ambient = new Color(90, 90, 95),
                Diffuse = new Color(190, 190, 200),
                Specular = new Color(60, 60, 60),
                Shininess = 16f,
            }
        );

        _bodiesBuilt = true;
        Console.WriteLine("Hierarchy: rebuilt sun/planet/moon.");
    }

    private void Select(Body body)
    {
        _selected = body;
        Console.WriteLine($"Hierarchy: selected {body} (speed {_speeds[(int)body]:0.00} rad/s).");
    }

    private void AdjustSpeed(float delta)
    {
        _speeds[(int)_selected] += delta;
        Console.WriteLine(
            $"Hierarchy: {_selected} speed now {_speeds[(int)_selected]:0.00} rad/s."
        );
    }

    // Destroys just the planet (not DestroyHierarchy) so the moon is orphaned to world-space per
    // docs/hierarchy.md: on the next TransformHierarchySystem.Update, the moon's Parent no longer
    // resolves, so its Parent component is removed and it keeps its last computed Transform3D
    // instead of moving or being destroyed.
    private void DestroyPlanet()
    {
        if (_world is not { } world || !_planetAlive)
            return;

        world.DestroyEntity(_planet);
        _planetAlive = false;
        Console.WriteLine(
            "Hierarchy: destroyed the planet — the moon is now orphaned to world-space."
        );
    }

    public void Update(float deltaTime)
    {
        if (_world is not { } world)
            return;

        // Rotation update: the sun spins about its own world Transform3D (it has no Parent), the
        // planet and moon spin about their LocalTransform3D — everything else (the planet
        // orbiting the sun, the moon orbiting the planet) falls out of composing those spins with
        // each ancestor's resolved world transform in TransformHierarchySystem below.
        if (world.TryGetComponent<Transform3D>(_sun, out var sunTransform))
        {
            var spin = Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                _speeds[(int)Body.Sun] * deltaTime
            );
            world.AddComponent(
                _sun,
                sunTransform with
                {
                    Rotation = Quaternion.Normalize(sunTransform.Rotation * spin),
                }
            );
        }

        if (_planetAlive && world.TryGetComponent<LocalTransform3D>(_planet, out var planetLocal))
        {
            var spin = Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                _speeds[(int)Body.Planet] * deltaTime
            );
            world.AddComponent(
                _planet,
                planetLocal with
                {
                    Rotation = Quaternion.Normalize(planetLocal.Rotation * spin),
                }
            );
        }

        if (world.TryGetComponent<LocalTransform3D>(_moon, out var moonLocal))
        {
            var spin = Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                _speeds[(int)Body.Moon] * deltaTime
            );
            world.AddComponent(
                _moon,
                moonLocal with
                {
                    Rotation = Quaternion.Normalize(moonLocal.Rotation * spin),
                }
            );
        }

        _hierarchySystem?.Update(deltaTime);
        _freeFlySystem?.Update(deltaTime);
    }

    public void Render(float deltaTime)
    {
        _meshRenderSystem?.Render();
        _inspector?.Render(deltaTime);
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.Num1);
        Keyboard.RemoveKeyDown(Keys.Num2);
        Keyboard.RemoveKeyDown(Keys.Num3);
        Keyboard.RemoveKeyDown(Keys.Plus);
        Keyboard.RemoveKeyDown(Keys.Minus);
        Keyboard.RemoveKeyDown(Keys.O);
        Keyboard.RemoveKeyDown(Keys.R);
        Keyboard.RemoveKeyDown(Keys.F1);

        _inspector?.Dispose();
        _renderer3D?.Dispose();
        _textures?.Dispose();
        _meshRegistry?.Dispose();
    }
}

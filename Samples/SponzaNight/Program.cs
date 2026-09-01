using System.Numerics;
using SponzaNight;
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

// SponzaNight — the integration proof for the "living Sponza" epic (#212), the way Samples/Platformer
// was for the platformer-support epic: everything the epic shipped (day/night cycle, procedural sky,
// distance fog, shaped/turbulent fire particles, light flicker, point-light shadows, 3D path
// following, locomotion-driven skeletal animation, pooled one-shot audio, HDR post-processing)
// composed into one scene that actually holds together. Samples/Sponza stays a minimal PBR reference;
// this is the "won't stay small" sample the issue's notes call for instead of growing that one.
//
// Scope (issue #228): an HDR post chain with bloom, four lit braziers (procedural bowl geometry +
// a shaped/flickering flame + a shadow-casting PointLight), a knight patrolling the atrium on a
// PathFollow3D route with speed-driven idle/walk blending and footstep SFX, and a full day/night
// cycle (sun + moon, ambient, fog, exposure) that can be scrubbed or frozen from the keyboard.
//
// Controls: WASD move, Q/E up/down, right-mouse-drag look, F1 toggle editor overlay,
// Left/Right scrub the time of day, Space freeze/resume the cycle,
// P toggle post-processing, B toggle bloom, T toggle tone-map operator, ESC exit.

var assetsDir = AssetPath.Resolve("Assets");
var sponzaPath = AssetPath.Resolve("Assets/Sponza/Sponza.gltf");
var knightPath = AssetPath.Resolve("Assets/Knight/KnightCharacter.fbx");

if (!File.Exists(sponzaPath) || !File.Exists(knightPath))
{
    Console.Error.WriteLine(
        $"Model(s) not found under {assetsDir}.\n"
            + "Run 'dotnet build' outside CI to fetch assets automatically."
    );
    return;
}

var sponzaScene = AssimpLoader.LoadScene(sponzaPath);
var knightScene = AssimpLoader.LoadScene(knightPath);
if (knightScene.Skeleton is not { } knightSkeleton)
{
    Console.Error.WriteLine("Knight model has no skeleton — nothing to animate.");
    return;
}

Console.WriteLine(
    $"Loaded Sponza ({sponzaScene.Meshes.Count} mesh(es)) and Knight "
        + $"({knightScene.Meshes.Count} mesh(es), {knightSkeleton.BoneCount} bone(s))"
);

using var window = Window.Create();
var world = new World();
using var meshRegistry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

// --- Static architecture -----------------------------------------------------------------------

foreach (var modelMesh in sponzaScene.Meshes)
{
    var entity = string.IsNullOrWhiteSpace(modelMesh.Name)
        ? world.CreateEntity()
        : world.CreateEntity(modelMesh.Name);
    world.AddComponent(entity, meshRegistry.Register(modelMesh.Mesh));
    world.AddComponent(entity, modelMesh.Transform);
    world.AddComponent(entity, Material3D.FromModel(modelMesh.Material));
    world.AddComponent(entity, modelMesh.Mesh.ToAabb());
}

// --- Camera --------------------------------------------------------------------------------------

var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(-11f, 3f, 3f),
        Target: new Vector3(5f, 1.2f, 0f),
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: 500f
    )
);
var freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 8f);

// --- Day/night cycle: sun + moon, ambient, fog, exposure ------------------------------------------

const float DayLengthSeconds = 120f;

var sunEntity = world.CreateEntity("sun");
world.AddComponent(
    sunEntity,
    new TimeOfDay
    {
        NormalizedTime = 0.7f, // dusk, heading toward night so the braziers read immediately
        DayLengthSeconds = DayLengthSeconds,
        AxisTilt = 0.35f,
    }
);
world.AddComponent(sunEntity, new CelestialLight(CelestialBody.Sun));

var moonEntity = world.CreateEntity("moon");
world.AddComponent(moonEntity, new CelestialLight(CelestialBody.Moon));

var dayNightCycle = new DayNightCycleSystem(world);

using var proceduralSkyRenderer = new ProceduralSkyRenderer(window.Gl);
var skyEntity = world.CreateEntity("sky");
world.AddComponent(skyEntity, ProceduralSky.Default);
var proceduralSkySystem = new ProceduralSkySystem(world);

var fogEntity = world.CreateEntity("fog");
world.AddComponent(fogEntity, FogSettings.Default with { Density = 0.018f });
var nightFog = new Color(18, 22, 40);
var dayFog = new Color(205, 198, 180);

// --- Shadows ---------------------------------------------------------------------------------

using var shadowMapRenderer = new ShadowMapRenderer(
    window.Gl,
    ShadowSettings.Default with
    {
        MapResolution = 2048,
        FarPlane = 500f,
        AutoFit = true, // frames the casters at every sun/moon angle instead of one fixed extent
        HorizonFadeElevation = 0.1f, // fades shadows out as the caster sets
        EnablePcf = true,
    }
);
using var pointShadowMapRenderer = new PointShadowMapRenderer(
    window.Gl,
    PointShadowSettings.Default
);

// --- Braziers: procedural bowl geometry + a shaped, flickering flame -----------------------------

const string particleTexture = "Assets/particle.png";

void AddBrazier(string tag, Vector3 basePosition, float flickerSeed)
{
    // Pedestal: a slightly tapered cylinder standing on the floor.
    var pedestalMesh = MeshFactory.CreateCylinder(
        $"{tag}_pedestal",
        0.22f,
        0.14f,
        1f,
        segments: 16
    );
    var pedestal = world.CreateEntity($"{tag}_pedestal");
    world.AddComponent(pedestal, meshRegistry.Register(pedestalMesh));
    world.AddComponent(
        pedestal,
        new Transform3D(basePosition + new Vector3(0f, 0.5f, 0f), Quaternion.Identity, Vector3.One)
    );
    world.AddComponent(
        pedestal,
        new Material3D
        {
            DiffuseTexturePath = string.Empty,
            Ambient = new Color(30, 28, 26),
            Diffuse = new Color(70, 65, 58),
            Specular = new Color(90, 85, 80),
            Shininess = 32f,
        }
    );
    world.AddComponent(pedestal, pedestalMesh.ToAabb());

    // Bowl: a wide, shallow, upward-flaring cylinder sitting on top of the pedestal.
    var bowlMesh = MeshFactory.CreateCylinder($"{tag}_bowl", 0.3f, 0.45f, 0.32f, segments: 20);
    var bowlPosition = new Vector3(basePosition.X, basePosition.Y + 1f + 0.16f, basePosition.Z);
    var bowl = world.CreateEntity($"{tag}_bowl");
    world.AddComponent(bowl, meshRegistry.Register(bowlMesh));
    world.AddComponent(bowl, new Transform3D(bowlPosition, Quaternion.Identity, Vector3.One));
    world.AddComponent(
        bowl,
        new Material3D
        {
            DiffuseTexturePath = string.Empty,
            Ambient = new Color(35, 30, 22),
            Diffuse = new Color(80, 68, 45),
            Specular = new Color(110, 100, 80),
            Shininess = 48f,
        }
    );
    world.AddComponent(bowl, bowlMesh.ToAabb());

    // Flame: emits from a disc filling the bowl rather than a jet from a point, rises with buoyancy,
    // and breaks up via turbulence instead of climbing in a straight line.
    var flamePosition = new Vector3(basePosition.X, basePosition.Y + 1f + 0.32f, basePosition.Z);
    var flame = world.CreateEntity($"{tag}_flame");
    world.AddComponent(flame, new Transform3D(flamePosition, Quaternion.Identity, Vector3.One));
    world.AddComponent(
        flame,
        new ParticleEmitter3D(particleTexture)
        {
            MaxParticles = 96,
            EmitRate = 45f,
            ParticleLifetime = 0.9f,
            LifetimeVariance = 0.25f,
            EmitDirection = Vector3.UnitY,
            SpreadAngle = MathF.PI / 8f,
            InitialSpeed = 0.6f,
            SpeedVariance = 0.3f,
            Shape = EmissionShape.Disc,
            DiscRadius = 0.26f,
            StartColor = new Color(255, 180, 60, 235),
            EndColor = new Color(120, 20, 0, 0),
            StartSize = 0.16f,
            EndSize = 0.04f,
            SizeVariance = 0.3f,
            RandomInitialRotation = true,
            Acceleration = new Vector3(0f, 1.5f, 0f), // buoyancy
            Drag = 0.8f,
            Turbulence = 0.5f,
            TurbulenceFrequency = 1.2f,
            BlendMode = MaterialBlendMode.Additive,
        }
    );

    // Light: sits at the bowl, flickers with its own seed so braziers don't pulse in lockstep, and
    // opts into a cube shadow map (only the two nearest the camera actually cast — see
    // Renderer3D.MaxShadowCastingPointLights).
    var light = world.CreateEntity($"{tag}_light");
    world.AddComponent(light, new Transform3D(flamePosition, Quaternion.Identity, Vector3.One));
    world.AddComponent(
        light,
        new PointLight
        {
            Color = new Color(255, 140, 50),
            Intensity = 3.2f,
            Range = 7f,
            CastsShadows = true,
        }
    );
    world.AddComponent(
        light,
        new LightFlicker
        {
            BaseIntensity = 3.2f,
            Amplitude = 1.1f,
            Frequency = 5f,
            Seed = flickerSeed,
            PositionJitter = 0.04f,
        }
    );
}

AddBrazier("brazier_nw", new Vector3(-6f, 0f, -3.5f), flickerSeed: 0f);
AddBrazier("brazier_ne", new Vector3(6f, 0f, -3.5f), flickerSeed: 1f);
AddBrazier("brazier_sw", new Vector3(-6f, 0f, 3.5f), flickerSeed: 2f);
AddBrazier("brazier_se", new Vector3(6f, 0f, 3.5f), flickerSeed: 3f);

var particleSystem3D = new ParticleSystem3D(world);
var lightFlickerSystem = new LightFlickerSystem(world);

// --- Knight: patrols the nave, blends idle/walk by speed, and plays footstep SFX -----------------

const string IdleClip = "HumanArmature|Idle";
const string WalkClip = "HumanArmature|Walking";

// The Knight FBX's mesh node and armature object each carry a baked object-level scale of 100 — a
// common "forgot to apply scale before export" artifact in Blender FBX exports. AssimpLoader now
// bakes a skinned mesh's own node transform into its vertex data (see AssimpLoader.ExtractMeshData
// and Assets/Knight.NOTICE.md), which corrects the mesh-node half of that; this constant compensates
// for the remaining armature-side scale, sized from the shipped SkeletalAnimationSystem's own
// resolved Aabb3D (~560 units tall post-fix) against a ~1.8m target height — confirmed by rendering
// the sample: the knight comes out a plausible, human-scale figure.
const float KnightScale = 0.0032f;

// Author footstep markers on the walk clip: not part of the source FBX (see
// docs/skeletal-animation.md — animation events are authored in code, not imported), placed at the
// two heel-strike points of the 1.25s cycle.
var knightAnimations = knightScene
    .Animations!.Select(clip =>
        clip.Name == WalkClip
            ? clip with
            {
                Events =
                [
                    new AnimationEventMarker(0.15f, "footstep"),
                    new AnimationEventMarker(0.775f, "footstep"),
                ],
            }
            : clip
    )
    .ToList();

var skeletonRegistry = new SkeletonRegistry();
var knightSkinnedVertices = knightScene.Meshes.SelectMany(m =>
    m.Mesh.Vertices.Select(v => new SkinnedVertex(v.Position, v.BoneIndices, v.BoneWeights))
);
var knightSkeletonHandle = skeletonRegistry.Register(
    knightSkeleton,
    knightAnimations,
    knightSkinnedVertices
);

var knightSubmeshes = knightScene
    .Meshes.Select(m =>
        (Handle: meshRegistry.Register(m.Mesh), Material: Material3D.FromModel(m.Material))
    )
    .ToList();

// A straight patrol down the open centre of the nave — clear of the colonnades that flank it on
// either side — turning around at each end (PingPong).
var knightWaypoints = new[] { new Vector3(-9f, 0f, 0f), new Vector3(9f, 0f, 0f) };
const float KnightSpeed = 1.4f;

var knightStates = new Dictionary<string, SkeletalState>
{
    ["idle"] = new SkeletalState(IdleClip),
    ["walk"] = new SkeletalState(WalkClip),
};
SpeedThreshold[] knightThresholds = [new("idle", MinSpeed: 0f), new("walk", MinSpeed: 0.3f)];

// Every submesh (Armor/Boots/Skin) gets its own entity carrying the full component set — identical
// PathFollow3D/state-machine parameters on each, so all three move and blend clips in lockstep
// (deterministic simulation from identical initial state) with no manual synchronization needed.
// The first submesh entity is treated as the "primary" one below for the one-shot footstep SFX,
// since all three would otherwise fire the same animation event independently.
var knightEntities = new List<Entity>();
foreach (var (handle, material) in knightSubmeshes)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, handle);
    world.AddComponent(entity, material);
    world.AddComponent(
        entity,
        new Transform3D(knightWaypoints[0], Quaternion.Identity, new Vector3(KnightScale))
    );
    world.AddComponent(
        entity,
        new PathFollow3D(
            knightWaypoints,
            speed: KnightSpeed,
            turnRate: MathF.PI,
            loopMode: TweenLoopMode.PingPong
        )
    );
    world.AddComponent(entity, knightSkeletonHandle);
    world.AddComponent(entity, new AnimationPlayer(IdleClip, loop: true, speed: 1f));
    world.AddComponent(
        entity,
        new SkeletalAnimationStateMachine(
            knightStates,
            initialState: "idle",
            speedThresholds: knightThresholds,
            speedHysteresis: 0.15f
        )
    );
    knightEntities.Add(entity);
}

var knightPrimaryEntity = knightEntities[0];
var pathFollowSystem = new PathFollow3DSystem(world);
var knightAnimationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);
var knightStateMachineSystem = new SkeletalAnimationStateMachineSystem(
    world,
    knightAnimationSystem
);

// --- Audio: pooled one-shot footstep SFX, triggered by the walk clip's event markers -------------

using var footstepBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/footstep.wav");
var audioSystem = new AudioSystem(world, window.AudioContext);

knightAnimationSystem.OnAnimationEvent += e =>
{
    // All three submesh entities carry the same clip/events and cross the marker in the same
    // frame; only react to the designated primary one so the sound doesn't fire three times.
    if (e.Key != "footstep" || e.Entity != knightPrimaryEntity)
        return;

    if (world.TryGetComponent<Transform3D>(knightPrimaryEntity, out var knightTransform))
        audioSystem.PlayOneShot(footstepBuffer, knightTransform.Position, gain: 0.5f);
};

// --- Post-processing: HDR scene render, bloom, tone mapping ---------------------------------------

var renderer3D = new Renderer3D(window.Gl, hdrOutput: true);
var meshRenderSystem = new MeshRenderSystem(
    renderer3D,
    meshRegistry,
    textures,
    world,
    window,
    shadowMapRenderer: shadowMapRenderer,
    proceduralSkyRenderer: proceduralSkyRenderer,
    pointShadowMapRenderer: pointShadowMapRenderer
);
var particleRenderSystem3D = new ParticleRenderSystem3D(
    renderer3D,
    textures,
    world,
    window,
    particleSystem3D
);

var postProcessStack = new PostProcessStack(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    sceneHasDepth: true,
    hdr: true
);
var bloom = new BloomEffect(
    window.Gl,
    (int)window.Size.X,
    (int)window.Size.Y,
    RenderTargetFormat.Rgba16F
)
{
    Threshold = 1f,
    Intensity = 1.3f,
};
var toneMap = new ToneMapEffect(window.Gl) { Operator = ToneMapOperator.AcesFilmic };
postProcessStack.Effects.Add(bloom);
postProcessStack.Effects.Add(toneMap);

using var inspector = new ImGuiInspector(window, world);

window.OnResize += size => postProcessStack.Resize((int)size.X, (int)size.Y);
window.OnClosing += () =>
{
    toneMap.Dispose();
    bloom.Dispose();
    postProcessStack.Dispose();
    renderer3D.Dispose();
    audioSystem.Dispose();
};

Console.WriteLine("SponzaNight");
Console.WriteLine(
    "WASD move | Q/E up/down | right-drag look | F1 editor | Left/Right scrub time | "
        + "Space freeze/resume cycle | P/B/T post-fx | ESC exit"
);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
Keyboard.AddKeyDown(
    Keys.B,
    () => Console.WriteLine($"Bloom {((bloom.Enabled = !bloom.Enabled) ? "ON" : "OFF")}")
);
Keyboard.AddKeyDown(
    Keys.P,
    () =>
        Console.WriteLine(
            $"Post-processing stack {((postProcessStack.Enabled = !postProcessStack.Enabled) ? "ON" : "OFF")}"
        )
);
Keyboard.AddKeyDown(
    Keys.T,
    () =>
    {
        toneMap.Operator =
            toneMap.Operator == ToneMapOperator.AcesFilmic
                ? ToneMapOperator.Reinhard
                : ToneMapOperator.AcesFilmic;
        Console.WriteLine($"Tone-map operator: {toneMap.Operator}");
    }
);

var cycleFrozen = false;
Keyboard.AddKeyDown(
    Keys.Space,
    () =>
    {
        cycleFrozen = !cycleFrozen;
        if (world.TryGetComponent<TimeOfDay>(sunEntity, out var time))
            world.AddComponent(
                sunEntity,
                time with
                {
                    DayLengthSeconds = cycleFrozen ? 0f : DayLengthSeconds,
                }
            );
        Console.WriteLine(cycleFrozen ? "Day/night cycle frozen" : "Day/night cycle resumed");
    }
);

var frameCount = 0;
var secondElapsed = 0.0;

window.OnUpdate += deltaTime =>
{
    var dt = (float)deltaTime;
    freeFlySystem.Update(dt);

    // Scrubbing: held Left/Right nudges the clock directly; DayNightCycleSystem.Update below then
    // applies whatever NormalizedTime the entity currently holds (including this nudge) on top of
    // its own per-frame advance (zero, while frozen).
    if (world.TryGetComponent<TimeOfDay>(sunEntity, out var scrubTime))
    {
        const float ScrubRate = 0.05f; // of a full day, per second held
        var scrub = 0f;
        if (Keyboard.IsKeyPressed(Keys.Right))
            scrub += ScrubRate * dt;
        if (Keyboard.IsKeyPressed(Keys.Left))
            scrub -= ScrubRate * dt;
        if (scrub != 0f)
            world.AddComponent(
                sunEntity,
                scrubTime with
                {
                    NormalizedTime = scrubTime.NormalizedTime + scrub,
                }
            );
    }

    dayNightCycle.Update(dt);
    proceduralSkySystem.Update(dt);

    if (world.TryGetComponent<FogSettings>(fogEntity, out var fog))
    {
        var t = dayNightCycle.CurrentLighting.DaylightFactor;
        var color = Color.FromVector4(Vector4.Lerp(nightFog.ToVector4(), dayFog.ToVector4(), t));
        world.AddComponent(fogEntity, fog with { Color = color });
    }
    toneMap.Exposure = dayNightCycle.CurrentLighting.Exposure;

    lightFlickerSystem.Update(dt);

    pathFollowSystem.Update(dt);
    knightStateMachineSystem.Update(dt);
    knightAnimationSystem.Update(dt);

    particleSystem3D.Update(dt);
    audioSystem.Update(dt);
};

window.OnRender += delta =>
{
    postProcessStack.Render(() =>
    {
        meshRenderSystem.Render();
        particleRenderSystem3D.Render();
    });
    inspector.Render(delta);

    frameCount++;
    secondElapsed += delta;
    if (secondElapsed >= 1.0)
    {
        Console.WriteLine(
            $"FPS: {frameCount} | draw calls: {renderer3D.DrawCallCount} main + "
                + $"{shadowMapRenderer.DrawCallCount} shadow"
        );
        frameCount = 0;
        secondElapsed = 0;
    }

    var screenshotPath = Environment.GetEnvironmentVariable("YAEGER_SCREENSHOT");
    if (screenshotPath is not null)
    {
        ScreenshotCapture.SaveFramebufferPng(window, screenshotPath);
        Console.WriteLine($"Screenshot saved to {screenshotPath}");
        window.Close();
    }
};

window.Run();

using System.Numerics;
using SequenceDemo;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Sequencing;
using Yaeger.Systems;
using Yaeger.Windowing;

// Sequence demo (issue #189): a directed, multi-beat cutscene — a lift descends, *then* (after a
// short settle) its two side doors slide open *in parallel*, *then* a reveal light kicks on — all
// authored as data via SequenceBuilder. There is no per-frame timer/state-machine code anywhere in
// this file: SequenceSystem drives the beats, and each beat that moves something uses TweenSystem
// (issue #188) under the hood via SequenceBuilder.StartTween/WaitForTweenFinished.
//
// Controls: SPACE skips the running sequence straight to its end (every remaining action still
// fires — the doors end up open and the light still comes on, just without waiting through the
// timings) — the same "nobody wants to sit through a 20-second lift ride on every run" shortcut
// docs/sequencing.md calls out. R restarts the whole cutscene from the top. ESC exits.

using var window = Window.Create();
var world = new World();
using var meshRegistry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

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

var liftTop = new Vector3(0f, 3f, 0f);
var liftBottom = new Vector3(0f, 0f, 0f);
var lift = MakeBox(
    "lift",
    liftTop,
    new Vector3(1.6f, 0.4f, 1.6f),
    new Color(22, 22, 25),
    new Color(90, 90, 100)
);

var leftDoorClosed = new Vector3(-0.9f, 0f, -1.6f);
var leftDoorOpen = new Vector3(-2.4f, 0f, -1.6f);
var leftDoor = MakeBox(
    "leftDoor",
    leftDoorClosed,
    new Vector3(1.2f, 2f, 0.2f),
    new Color(30, 17, 15),
    new Color(120, 70, 60)
);

var rightDoorClosed = new Vector3(0.9f, 0f, -1.6f);
var rightDoorOpen = new Vector3(2.4f, 0f, -1.6f);
var rightDoor = MakeBox(
    "rightDoor",
    rightDoorClosed,
    new Vector3(1.2f, 2f, 0.2f),
    new Color(30, 17, 15),
    new Color(120, 70, 60)
);

var revealLight = world.CreateEntity("revealLight");
world.AddComponent(
    revealLight,
    new Transform3D(new Vector3(0f, 3f, -1.6f), Quaternion.Identity, Vector3.One)
);
world.AddComponent(
    revealLight,
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

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, meshRegistry, textures, world, window);
var tweenSystem = new TweenSystem(world);
var sequenceSystem = new SequenceSystem();

// Build once; Play() creates fresh progress each time, so R can safely re-play the same
// definition without rebuilding it.
var cutscene = new SequenceBuilder(world)
    .StartTween(
        lift,
        Tween.Create(
            lift,
            TweenChannel.Transform3DPosition,
            liftTop,
            liftBottom,
            duration: 2f,
            easing: EasingFunction.CubicInOut
        )
    )
    .WaitForTweenFinished(lift)
    .WaitForSeconds(0.4f) // a short settle before the doors move
    .Parallel(
        branch =>
            branch
                .StartTween(
                    leftDoor,
                    Tween.Create(
                        leftDoor,
                        TweenChannel.Transform3DPosition,
                        leftDoorClosed,
                        leftDoorOpen,
                        duration: 1f,
                        easing: EasingFunction.SineInOut
                    )
                )
                .WaitForTweenFinished(leftDoor),
        branch =>
            branch
                .StartTween(
                    rightDoor,
                    Tween.Create(
                        rightDoor,
                        TweenChannel.Transform3DPosition,
                        rightDoorClosed,
                        rightDoorOpen,
                        duration: 1f,
                        easing: EasingFunction.SineInOut
                    )
                )
                .WaitForTweenFinished(rightDoor)
    )
    .StartTween(
        revealLight,
        Tween.Create(
            revealLight,
            TweenChannel.PointLightIntensity,
            0f,
            3f,
            duration: 0.8f,
            easing: EasingFunction.SineOut
        )
    )
    .Callback(() => Console.WriteLine("Reveal!"))
    .Build();

var handle = sequenceSystem.Play(cutscene);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.Space, () => sequenceSystem.SkipToEnd(handle));
Keyboard.AddKeyDown(
    Keys.R,
    () =>
    {
        // Snap every animated entity back to its starting pose before replaying — otherwise the
        // new playback's first StartTween would animate from wherever the previous run left off.
        world.AddComponent(
            lift,
            new Transform3D(
                liftTop,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(lift).Scale
            )
        );
        world.AddComponent(
            leftDoor,
            new Transform3D(
                leftDoorClosed,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(leftDoor).Scale
            )
        );
        world.AddComponent(
            rightDoor,
            new Transform3D(
                rightDoorClosed,
                Quaternion.Identity,
                world.GetComponent<Transform3D>(rightDoor).Scale
            )
        );
        world.AddComponent(
            revealLight,
            world.GetComponent<PointLight>(revealLight) with
            {
                Intensity = 0f,
            }
        );
        handle = sequenceSystem.Play(cutscene);
    }
);

window.OnUpdate += deltaTime =>
{
    sequenceSystem.Update((float)deltaTime);
    tweenSystem.Update((float)deltaTime);
};

window.OnRender += _ => meshRenderSystem.Render();

window.Run();

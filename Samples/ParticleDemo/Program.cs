using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Particle demo: a fire fountain and a smoke plume burn continuously, and clicking
// anywhere triggers a short spark explosion at the mouse position. All particles go
// through the renderer's existing batched quad path — sharing one texture, the whole
// scene collapses into a handful of draw calls.
//
// Controls:
//   Left click — spawn an explosion at the cursor
//   ESC        — exit

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var renderSystem = new UnifiedRenderSystem(renderer, null, world);
var particleSystem = new ParticleSystem(world, renderer);

const string particleTexture = "Assets/particle.png";

// Fire fountain: fast, narrow cone, bright yellow fading to transparent red, shrinking.
var fire = world.CreateEntity("fire");
world.AddComponent(fire, new Transform2D(new Vector2(0.3f, -0.7f)));
world.AddComponent(
    fire,
    new ParticleEmitter(particleTexture)
    {
        MaxParticles = 512,
        EmitRate = 150f,
        ParticleLifetime = 1.2f,
        EmitDirection = new Vector2(0f, 1f),
        SpreadAngle = MathF.PI / 5f,
        InitialSpeed = 0.6f,
        StartColor = new Color(255, 200, 40),
        EndColor = new Color(255, 30, 0, 0),
        StartSize = 0.07f,
        EndSize = 0.015f,
    }
);

// Smoke plume: slow, wide cone, grey growing and dissolving.
var smoke = world.CreateEntity("smoke");
world.AddComponent(smoke, new Transform2D(new Vector2(-0.5f, -0.5f)));
world.AddComponent(
    smoke,
    new ParticleEmitter(particleTexture)
    {
        MaxParticles = 256,
        EmitRate = 35f,
        ParticleLifetime = 2.5f,
        EmitDirection = new Vector2(0.15f, 1f),
        SpreadAngle = MathF.PI / 3f,
        InitialSpeed = 0.25f,
        StartColor = new Color(180, 180, 180, 200),
        EndColor = new Color(70, 70, 70, 0),
        StartSize = 0.05f,
        EndSize = 0.2f,
    }
);

// Explosions: short-lived emitters spawned at the cursor. Each one emits radially for a
// brief burst window, then stops emitting and lingers until its sparks have died.
const float explosionEmitDuration = 0.12f;
const float explosionTotalDuration = 1.0f;
var explosions = new List<(Entity Entity, float Age)>();

Mouse.AddButtonDown(
    MouseButton.Left,
    () =>
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Mouse.PositionNdc));
        world.AddComponent(
            entity,
            new ParticleEmitter(particleTexture)
            {
                MaxParticles = 256,
                EmitRate = 2000f,
                ParticleLifetime = 0.7f,
                EmitDirection = Vector2.Zero,
                SpreadAngle = MathF.Tau, // full circle
                InitialSpeed = 1.1f,
                StartColor = new Color(255, 240, 120),
                EndColor = new Color(255, 60, 0, 0),
                StartSize = 0.035f,
                EndSize = 0.005f,
            }
        );
        explosions.Add((entity, 0f));
    }
);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnUpdate += deltaTime =>
{
    var dt = (float)deltaTime;
    particleSystem.Update(dt);

    for (var i = explosions.Count - 1; i >= 0; i--)
    {
        var (entity, age) = explosions[i];
        age += dt;

        if (age >= explosionTotalDuration)
        {
            world.DestroyEntity(entity);
            explosions.RemoveAt(i);
            continue;
        }

        if (age >= explosionEmitDuration)
        {
            var emitter = world.GetComponent<ParticleEmitter>(entity);
            if (emitter.EmitRate > 0f)
            {
                emitter.EmitRate = 0f;
                world.AddComponent(entity, emitter);
            }
        }

        explosions[i] = (entity, age);
    }
};

window.OnRender += _ =>
{
    renderSystem.Render();
    particleSystem.Render();
};

window.OnClosing += () =>
{
    renderer.Dispose();
};

window.Run();

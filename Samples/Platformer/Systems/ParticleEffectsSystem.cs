using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Platformer.Systems;

/// <summary>
/// Ground-feel and pickup particles built on the engine's <see cref="ParticleSystem"/>: a
/// continuous puff while the player runs on the ground, a one-shot burst the instant it lands,
/// and a one-shot sparkle burst wherever a coin is collected. Burst emitters follow the same
/// "emit for a short window, then stop and let the pool drain" pattern as
/// <c>Samples/ParticleDemo</c>'s click-triggered explosions — set <c>EmitRate</c> to zero after
/// <see cref="BurstEmitDuration"/>, then destroy the entity after <see cref="BurstTotalDuration"/>.
/// </summary>
public sealed class ParticleEffectsSystem
{
    private const float BurstEmitDuration = 0.1f;
    private const float BurstTotalDuration = 0.6f;

    private readonly World _world;
    private readonly ParticleSystem _particleSystem;
    private readonly Entity _runDustEmitter;
    private readonly List<(Entity Entity, float Age)> _bursts = [];
    private bool _wasGrounded;

    public ParticleEffectsSystem(World world, IRenderSurface? renderer)
    {
        _world = world;
        _particleSystem = new ParticleSystem(world, renderer);

        _runDustEmitter = world.CreateEntity();
        world.AddComponent(_runDustEmitter, new Transform2D(Vector2.Zero));
        world.AddComponent(
            _runDustEmitter,
            new ParticleEmitter("Assets/particle.png")
            {
                MaxParticles = 64,
                EmitRate = 0f,
                ParticleLifetime = 0.35f,
                EmitDirection = new Vector2(0f, 1f),
                SpreadAngle = MathF.PI / 2.2f,
                InitialSpeed = 0.5f,
                StartColor = new Color(210, 190, 150, 180),
                EndColor = new Color(210, 190, 150, 0),
                StartSize = 0.06f,
                EndSize = 0.02f,
            }
        );
    }

    /// <summary>
    /// Advances the run-dust emitter (following the player's feet, active while grounded and
    /// moving) and ages/destroys expired burst emitters. Spawns a landing puff the frame the
    /// player transitions from airborne to grounded.
    /// </summary>
    public void Update(
        float deltaTime,
        Vector2 feetPosition,
        bool isGrounded,
        float horizontalSpeed
    )
    {
        var transform = _world.GetComponent<Transform2D>(_runDustEmitter);
        transform.Position = feetPosition;
        _world.AddComponent(_runDustEmitter, transform);

        var emitter = _world.GetComponent<ParticleEmitter>(_runDustEmitter);
        var shouldEmit = isGrounded && MathF.Abs(horizontalSpeed) > 0.5f;
        emitter.EmitRate = shouldEmit ? 30f : 0f;
        _world.AddComponent(_runDustEmitter, emitter);

        if (isGrounded && !_wasGrounded)
            SpawnBurst(
                feetPosition,
                new Color(210, 190, 150, 200),
                new Color(210, 190, 150, 0),
                new Vector2(0f, 1f),
                MathF.PI, // upward fan, kicked up from the contact point
                speed: 1.2f,
                size: 0.09f
            );

        _wasGrounded = isGrounded;

        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            var (entity, age) = _bursts[i];
            age += deltaTime;

            if (age >= BurstTotalDuration)
            {
                _world.DestroyEntity(entity);
                _bursts.RemoveAt(i);
                continue;
            }

            if (age >= BurstEmitDuration)
            {
                var burstEmitter = _world.GetComponent<ParticleEmitter>(entity);
                if (burstEmitter.EmitRate > 0f)
                {
                    burstEmitter.EmitRate = 0f;
                    _world.AddComponent(entity, burstEmitter);
                }
            }

            _bursts[i] = (entity, age);
        }

        _particleSystem.Update(deltaTime);
    }

    /// <summary>Spawns a short golden sparkle burst at a collected coin's position.</summary>
    public void SpawnCoinSparkle(Vector2 position) =>
        SpawnBurst(
            position,
            new Color(255, 235, 120),
            new Color(255, 200, 0, 0),
            Vector2.Zero,
            MathF.Tau, // full circle
            speed: 1.5f,
            size: 0.045f
        );

    public void Render() => _particleSystem.Render();

    private void SpawnBurst(
        Vector2 position,
        Color startColor,
        Color endColor,
        Vector2 emitDirection,
        float spreadAngle,
        float speed,
        float size
    )
    {
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Transform2D(position));
        _world.AddComponent(
            entity,
            new ParticleEmitter("Assets/particle.png")
            {
                MaxParticles = 64,
                EmitRate = 400f,
                ParticleLifetime = BurstTotalDuration - BurstEmitDuration,
                EmitDirection = emitDirection,
                SpreadAngle = spreadAngle,
                InitialSpeed = speed,
                StartColor = startColor,
                EndColor = endColor,
                StartSize = size,
                EndSize = size * 0.3f,
            }
        );
        _bursts.Add((entity, 0f));
    }
}

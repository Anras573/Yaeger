using System.Numerics;
using Platformer.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Platformer.Systems;

/// <summary>
/// Detects overlaps between the player and coins/enemies/the goal flag via simple AABB checks,
/// since the player uses <see cref="CharacterController2D"/> — which deliberately bypasses the
/// <c>BoxCollider2D</c>/<c>PhysicsWorld2D</c> collider pipeline (see its remarks) — so none of
/// this can ride on <c>PhysicsWorld2D.OnCollisionEnter</c>. Game code decides what "hit" means
/// for each kind of entity, exactly as the engine's docs describe for composing systems.
/// </summary>
public class PlayerInteractionSystem(World world, Entity player)
{
    /// <summary>Upward velocity applied to the player immediately after stomping an enemy.</summary>
    public float StompBounceVelocity { get; set; } = 6f;

    public event Action? CoinCollected;
    public event Action? EnemyStomped;
    public event Action? PlayerHurt;
    public event Action? GoalReached;

    public void Update()
    {
        if (!world.TryGetComponent<Transform2D>(player, out var playerTransform))
            return;
        if (!world.TryGetComponent<CharacterController2D>(player, out var controller))
            return;
        world.TryGetComponent<Velocity2D>(player, out var velocity);

        var playerCenter = playerTransform.Position + controller.Offset;
        var playerHalf = controller.HalfSize;

        foreach (var (entity, coin, coinTransform) in world.Query<Coin, Transform2D>().ToList())
        {
            if (!Overlaps(playerCenter, playerHalf, coinTransform.Position, coin.HalfSize))
                continue;

            world.DestroyEntity(entity);
            CoinCollected?.Invoke();
        }

        foreach (var (entity, enemy, enemyTransform) in world.Query<Enemy, Transform2D>().ToList())
        {
            if (!Overlaps(playerCenter, playerHalf, enemyTransform.Position, enemy.HalfSize))
                continue;

            // A stomp requires the player to be falling and contacting from above — otherwise
            // this is a side/underneath hit and the player takes damage instead.
            var playerBottom = playerCenter.Y - playerHalf.Y;
            var isStomp = velocity.Linear.Y < 0f && playerBottom > enemyTransform.Position.Y;

            if (isStomp)
            {
                world.DestroyEntity(entity);
                velocity.Linear.Y = StompBounceVelocity;
                world.AddComponent(player, velocity);
                EnemyStomped?.Invoke();
            }
            else
            {
                PlayerHurt?.Invoke();
            }
        }

        foreach (var (_, goal, goalTransform) in world.Query<Goal, Transform2D>())
        {
            if (Overlaps(playerCenter, playerHalf, goalTransform.Position, goal.HalfSize))
                GoalReached?.Invoke();
        }
    }

    private static bool Overlaps(Vector2 centerA, Vector2 halfA, Vector2 centerB, Vector2 halfB) =>
        MathF.Abs(centerA.X - centerB.X) < halfA.X + halfB.X
        && MathF.Abs(centerA.Y - centerB.Y) < halfA.Y + halfB.Y;
}

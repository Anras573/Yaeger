using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Generates static <see cref="BoxCollider2D"/> entities from a <see cref="Tilemap"/>'s solid
/// tiles (see <see cref="Tileset.IsSolid"/>), merging adjacent solid tiles into the fewest
/// axis-aligned rectangles via <see cref="TilemapColliderMerger"/> instead of emitting one
/// collider per tile. This avoids both the broadphase cost of thousands of per-tile colliders
/// and the seam-snagging that comes from a moving body picking up a spurious collision normal
/// from an internal edge between two adjacent solid tiles.
/// </summary>
/// <remarks>
/// Only axis-aligned, unrotated tilemaps are supported — <see cref="Transform2D.Scale"/> and
/// <see cref="Transform2D.Rotation"/> are ignored, matching <c>BoxCollider2D</c>'s own lack of
/// rotation support.
/// </remarks>
public sealed class TilemapColliderSystem(World world) : IUpdateSystem
{
    private readonly Dictionary<Entity, int[]> _lastTiles = new();
    private readonly Dictionary<Entity, List<Entity>> _generatedColliders = new();
    private readonly List<Entity> _staleTilemapEntities = [];

    /// <summary>
    /// Rebuilds collider entities for every <see cref="Tilemap"/> whose tile contents changed
    /// since the last call (including the first call, for initial generation), and discards
    /// tracking state for tilemap entities that were destroyed.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Snapshot before mutating the world (creating/destroying entities) so we never
        // mutate a component store while WorldExtensions.Query is enumerating it.
        var tilemaps = world.Query<Tilemap, Transform2D>().ToList();

        var seen = new HashSet<Entity>();
        foreach (var (entity, tilemap, transform) in tilemaps)
        {
            seen.Add(entity);
            if (HasChanged(entity, tilemap))
                Rebuild(entity, tilemap, transform);
        }

        RemoveStaleTilemaps(seen);
    }

    private bool HasChanged(Entity entity, in Tilemap tilemap)
    {
        if (
            _lastTiles.TryGetValue(entity, out var cached)
            && cached.Length == tilemap.Tiles.Length
            && cached.AsSpan().SequenceEqual(tilemap.Tiles)
        )
            return false;

        _lastTiles[entity] = (int[])tilemap.Tiles.Clone();
        return true;
    }

    private void Rebuild(Entity tilemapEntity, in Tilemap tilemap, in Transform2D transform)
    {
        var colliders = GetOrCreateColliderList(tilemapEntity);
        foreach (var colliderEntity in colliders)
            world.DestroyEntity(colliderEntity);
        colliders.Clear();

        var width = tilemap.Width;
        var height = tilemap.Height;
        var tileset = tilemap.Tileset;
        var tiles = tilemap.Tiles;

        var rectangles = TilemapColliderMerger.Merge(
            width,
            height,
            (column, row) => tileset.IsSolid(tiles[row * width + column])
        );

        foreach (var rect in rectangles)
        {
            var size = new Vector2(
                rect.Width * tilemap.TileSize.X,
                rect.Height * tilemap.TileSize.Y
            );
            var bottomLeft =
                transform.Position
                + new Vector2(
                    rect.Column * tilemap.TileSize.X,
                    (height - rect.Row - rect.Height) * tilemap.TileSize.Y
                );
            var center = bottomLeft + size / 2.0f;

            var colliderEntity = world.CreateEntity();
            world.AddComponent(colliderEntity, new Transform2D(center));
            world.AddComponent(colliderEntity, new BoxCollider2D(size));
            world.AddComponent(colliderEntity, RigidBody2D.CreateStatic());
            colliders.Add(colliderEntity);
        }
    }

    private List<Entity> GetOrCreateColliderList(Entity tilemapEntity)
    {
        if (!_generatedColliders.TryGetValue(tilemapEntity, out var colliders))
        {
            colliders = [];
            _generatedColliders[tilemapEntity] = colliders;
        }

        return colliders;
    }

    private void RemoveStaleTilemaps(HashSet<Entity> seen)
    {
        _staleTilemapEntities.Clear();
        foreach (var entity in _lastTiles.Keys)
        {
            if (!seen.Contains(entity))
                _staleTilemapEntities.Add(entity);
        }

        foreach (var entity in _staleTilemapEntities)
        {
            _lastTiles.Remove(entity);
            if (_generatedColliders.Remove(entity, out var colliders))
            {
                foreach (var colliderEntity in colliders)
                    world.DestroyEntity(colliderEntity);
            }
        }
    }
}

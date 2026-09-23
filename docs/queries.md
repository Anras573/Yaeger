# 3D Queries

`World.Raycast`/`RaycastAll` (in `Yaeger.Physics`, via `WorldRaycastExtensions`) answer "what is
along this line" for a 3D scene: does this projectile hit that character, what is the camera
looking at, can this entity see that one. This is deliberately **not** 3D physics — there are no
rigid bodies, no collision resolution, no gravity. It is a single query API against the
`Aabb3D` bounds the engine already tracks for frustum culling and shadow mapping. See
`docs/physics.md` for the (entirely separate) 2D physics simulation.

```csharp
var ray = new Ray3D(camera.Position, cameraForward);

if (world.Raycast(ray, maxDistance: 100f) is { } hit)
{
    Console.WriteLine($"Hit {hit.Entity} at {hit.Point}, {hit.Distance} units away");
}
```

## `Ray3D`

An origin point and a direction, normalized on construction — the constructor throws
`ArgumentOutOfRangeException` for a zero-length or non-finite direction, so every query built on
top of it can assume unit length rather than re-checking it.

## `World.Raycast` / `RaycastAll`

- `world.Raycast(ray, maxDistance = ∞, mask = AllLayers)` returns the nearest hit as a nullable
  `RaycastHit` (`Entity`, `Distance`, `Point`, `Normal`), or `null` when nothing is hit.
- `world.RaycastAll(ray, maxDistance = ∞, mask = AllLayers)` returns every hit within
  `maxDistance`, sorted nearest-first (empty when nothing is hit).
- Both test every entity carrying `Transform3D` + `Aabb3D`, treating the box in the entity's own
  local space via `Transform3D.ModelMatrix` — so a rotated or non-uniformly scaled box is tested
  as the oriented box it actually is, not a re-bounded world-space AABB (the same choice
  `Inspector.ViewportPicking` already made for click-to-select).
- `Distance` is measured in the ray's own units (finite, since `Ray3D.Direction` is unit-length).
  A ray that starts inside a box reports the *exit* point instead of `0`, so "am I inside
  something" isn't indistinguishable from "I didn't hit anything".
- `Normal` is the outward-facing surface normal at the hit point (the exit face's normal when the
  ray started inside the box), transformed by the model's inverse-transpose so it stays correct
  under non-uniform scale.

### Layer/mask filtering

`Aabb3D.Layer` (an `int` bit index, `[0, 31]`, defaulting to `0`) mirrors
`BoxCollider2D.Layer`/`CollidesWith`'s convention from 2D physics. `mask` is a bitmask of which
layers a query should hit; an entity is only tested when `mask` includes its `Aabb3D.Layer` bit.
The default `mask` (`WorldRaycastExtensions.AllLayers`) matches every layer, so an unconfigured
box (`Layer = 0`) is hit by default — the same "opt out, don't opt in" default 2D colliders use.

```csharp
const uint EnemyLayer = 1u << 2;

var enemyAabb = new Aabb3D(min, max) { Layer = 2 };
world.AddComponent(enemyEntity, enemyAabb);

// Only test entities on the enemy layer.
var hit = world.Raycast(ray, mask: EnemyLayer);
```

## Performance

Both queries do a linear scan over every `Transform3D` + `Aabb3D` entity (via
`WorldExtensions.Query<Transform3D, Aabb3D>`) — no broadphase. This is fine for typical scene
sizes; a spatial structure (BVH, spatial hash) is the natural follow-up if profiling shows a
scene's entity count makes the linear scan a bottleneck.

## Shared implementation with viewport picking

The ray-vs-oriented-box slab test lives in `Yaeger.Physics.RayAabbIntersection` (`Yaeger.Core`,
no rendering dependency) so it can be unit-tested independent of a live window.
`Inspector.ViewportPicking.TryIntersectRayAabb` (the editor's click-to-select) is a thin wrapper
over the same method — one implementation instead of two copies of the slab test, so world
queries and viewport picking agree on every edge case (ray starting inside the box, a ray
parallel to a slab, zero-thickness bounds).

## See it in action

`Samples/FeatureGallery` — `--scene Raycast`: a field of boxes across two layers. LMB fires
`Raycast` (tints the nearest hit and drops a marker oriented along its normal); Shift+LMB fires
`RaycastAll` (tints every box along the ray and reports the hit count/distances on the HUD); `M`
cycles the layer mask. The unit tests are the other runnable reference:
`tests/Yaeger.Tests/Physics/WorldRaycastExtensionsTests.cs`.

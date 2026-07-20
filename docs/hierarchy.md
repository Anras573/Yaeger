# Entity Hierarchy

`Parent` + `TransformHierarchySystem` let one entity's transform move with another's — a turret
that stays mounted on a tank hull, a sword that follows a hand bone, a health bar that hovers
above an enemy. Without it, every one of those is hand-rolled position copying in game code.

## The design

`Transform2D`/`Transform3D` stay **world-space everywhere** — every renderer, physics system, and
camera in the engine reads them exactly as it always has, with zero hierarchy-awareness added.
Only a hierarchy *child* needs anything extra: a `Parent` component (a reference to the entity
it's relative to) plus a `LocalTransform2D`/`LocalTransform3D` (its position/rotation/scale in the
parent's local space, not world space).

Each update, `TransformHierarchySystem`:

1. Walks the `Parent` chain for every entity that has one, ordering parents before children (and
   throwing `InvalidOperationException` if the chain loops back on itself instead of hanging).
2. Composes each child's `LocalTransform2D`/`LocalTransform3D` with its parent's *already-resolved*
   world `Transform2D`/`Transform3D`.
3. Writes the result into the child's own `Transform2D`/`Transform3D` — so every other system in
   the engine keeps reading a plain world-space transform, unaware a hierarchy is involved.

There's no separate child-list component; children are found by querying for `Parent`, and the
system builds and caches the traversal order itself.

## Adding a two-level hierarchy

```csharp
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

var world = new World();

// The tank hull — a normal, world-space root entity.
var tank = world.CreateEntity("tank");
world.AddComponent(tank, new Transform2D(new Vector2(2f, 0f), rotation: 0f));

// The turret — parented to the tank, positioned relative to it.
var turret = world.CreateEntity("turret");
world.AddComponent(turret, new Parent(tank));
world.AddComponent(turret, new LocalTransform2D(new Vector2(0f, 0.2f))); // sits above the hull

var hierarchySystem = new TransformHierarchySystem(world);

// Drive the hull however gameplay code normally would...
world.AddComponent(tank, new Transform2D(new Vector2(5f, 0f), rotation: MathF.PI / 2));

// ...then resolve children before anything renders or reads Transform2D this frame.
hierarchySystem.Update(deltaTime: 1f / 60f);

// turret.Transform2D is now world-space: rotated and translated with the hull.
world.TryGetComponent<Transform2D>(turret, out var turretWorld);
```

The 3D shape is identical, swapping in `Transform3D`/`LocalTransform3D` (rotation is a
`Quaternion` there instead of a single `float`).

**Run order matters**: call `TransformHierarchySystem.Update` *after* whatever gameplay/physics
code updates a parent's own `Transform2D`/`Transform3D` and each child's local transform, and
*before* any render system reads `Transform2D`/`Transform3D` for the frame — the same convention
as `CameraFollowSystem` (see [camera.md](camera.md)).

## Composition semantics

Composition is scale-then-rotate-then-translate, matching `Transform2D.TransformMatrix`/
`Transform3D.ModelMatrix`'s own convention:

- World position = parent's world position + (child's local position, scaled by the parent's
  world scale, then rotated by the parent's world rotation).
- World rotation = parent's world rotation + child's local rotation (2D, both about Z), or
  parent's world rotation `*` child's local rotation (3D quaternions).
- World scale = parent's world scale `*` child's local scale, component-wise.

Like most 2D/3D scene graphs, this does not model the shear a fully exact matrix decomposition
would introduce when non-uniform scale and rotation combine across several levels of the
hierarchy — for the common case of uniform (or leaf-only) scale, it is exact.

## Orphaning and destruction

Destroying a parent does **not** destroy its children. On the next `TransformHierarchySystem.Update`,
any child whose `Parent` no longer resolves to an entity carrying the matching world transform —
because the parent was destroyed, or never had one — is *orphaned to world-space*: its `Parent`
component is removed, and its last computed `Transform2D`/`Transform3D` is left exactly where it
was. If you want cascading destruction instead, walk the `Parent` graph yourself before calling
`world.DestroyEntity` on the parent — that's a deliberately simple, opt-in helper rather than
default behavior.

## Prefabs and scenes

`Parent` is registered with `ComponentRegistry.RegisterEngineComponents()` like any other
component, so it round-trips through prefab and scene JSON:

```json
{ "type": "Parent", "parentTag": "tank" }
```

`parentTag` is resolved via the entity's tag — the only cross-entity reference the prefab/scene
format supports. Inside a scene file, the referenced tag can belong to an entity defined *anywhere*
in the file, including later than the child — `Scene.Apply` creates every entity (and registers
its tag) before applying any component, so forward references just work. Inside a prefab, the
tag must already exist on the target `World` before you call `world.Instantiate(prefab, ...)`,
since a prefab only ever describes a single entity.

Saving a `Parent` back out requires the parent entity to be tagged; `SceneSaver` throws
`SceneSaveException` if it isn't, since there's no other way to express the reference in JSON.

## Out of scope

- **Hierarchy-aware physics** — colliders stay world-space; a parented `BoxCollider2D`/
  `CircleCollider2D` does not automatically follow its parent's motion.
- **Editor drag-to-reparent** — the `ImGuiInspector` overlay doesn't have a hierarchy UI yet.

## See also

- `src/Engine/Yaeger/ECS/Parent.cs`
- `src/Engine/Yaeger/Systems/TransformHierarchySystem.cs`
- `src/Engine/Yaeger/Graphics/LocalTransform2D.cs`, `LocalTransform3D.cs`
- [scenes.md](scenes.md) — the prefab/scene JSON pipeline `Parent` plugs into

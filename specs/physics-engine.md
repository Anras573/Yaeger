# Physics Engine Spec

A custom, lightweight 2D physics engine for the Yaeger game engine (Yet Another Experimental Game Engine Repository). The engine integrates with the existing ECS architecture and is designed with future 3D extensibility in mind.

## Design Principles

- **Custom implementation** rather than integrating an external library — the engine is 2D, experimental, and ECS-based, making a custom solution the best fit.
- **Dimensionality-agnostic types** where possible (e.g., `PhysicsMaterial`, `BodyType`) so they can be reused when 3D support is added later.
- **ECS conventions** — components as `struct` (enforced by ECS), systems as classes taking `World` via primary constructor.
- **Math** — `System.Numerics` for vectors and math operations.
- **Testing** — xUnit with Arrange/Act/Assert pattern.

## Phases

### Phase 1: 2D Basics (Complete)

Core 2D physics with brute-force collision detection.

**Components:**

- `RigidBody2D` — Mass, inverse mass, body type, linear drag. Factory methods for Dynamic/Static/Kinematic creation. Validates mass > 0 for dynamic bodies and linearDrag >= 0.
- `Velocity2D` — Linear velocity (Vector2) and angular velocity (float).
- `BoxCollider2D` — Axis-aligned box defined by size and offset. Validates width/height > 0.
- `CircleCollider2D` — Circle defined by radius and offset. Validates radius > 0.
- `PhysicsMaterial` — Restitution (bounciness, clamped to [0,1]) and friction (>= 0). Default: restitution 0.3, friction 0.4.
- `BodyType` — Enum: Dynamic, Static, Kinematic. Dimensionality-agnostic.

**Systems:**

- `GravitySystem` — Applies gravity to dynamic bodies. Iterates `RigidBody2D` store, writes `Velocity2D`.
- `MovementSystem` — Integrates velocity into position (semi-implicit Euler). Applies linear drag with clamped factor. Iterates `RigidBody2D` store, writes `Transform2D` and `Velocity2D`.
- `CollisionDetectionSystem` — Brute-force O(n^2) narrowphase. Supports Box-Box (AABB), Circle-Circle, and Box-Circle pairs. Reuses entity lists across frames to avoid per-frame allocations.
- `CollisionResolutionSystem` — Impulse-based resolution with Coulomb friction and positional correction (Baumgarte stabilization). Falls back to `PhysicsMaterial.Default` when component is missing.

**Facade:**

- `PhysicsWorld2D` — Orchestrates all systems in order: Gravity -> Movement -> Detection -> Resolution. Exposes collision events with thread-safe invocation.
- `CollisionManifold` — Contact data struct: entity pair, normal (unit vector), penetration depth, contact point.

### Phase 2: Broadphase Spatial Partitioning (Future)

Replace the O(n^2) brute-force broadphase with spatial data structures to improve performance for scenes with many entities.

- Uniform grid spatial partitioning
- QuadTree for non-uniform entity distributions
- Broadphase/narrowphase separation — broadphase produces candidate pairs, narrowphase confirms collisions

### Phase 3: 3D Foundation (Future)

Extend the physics engine to support 3D simulations, reusing dimensionality-agnostic types from Phase 1.

- `RigidBody3D` — 3D rigid body component (reuses `BodyType`, `PhysicsMaterial`)
- `BoxCollider3D` — Axis-aligned bounding box in 3D
- `SphereCollider3D` — Sphere collider
- Octree for 3D spatial partitioning
- GJK and/or SAT algorithms for convex collision detection

### Phase 4: Advanced Features

- **Collision layers, masks, and trigger colliders (Complete)** — `BoxCollider2D`/`CircleCollider2D`
  carry `Layer` (int, [0, 31]) and `CollidesWith` (bitmask), defaulting to collide-with-everything.
  `CollisionDetectionSystem` filters candidate pairs by a symmetric layer/mask check before
  narrowphase. `IsTrigger` marks a collider as a non-resolving sensor: its manifolds are still
  produced and reported via `OnCollision`, but `CollisionResolutionSystem` skips them.
- **Collision enter/exit/stay events (Complete)** — `PhysicsWorld2D` keeps the previous step's
  contact pair set (keyed order-independently on the two entity IDs) and diffs it against the
  current step's manifolds. `OnCollisionEnter` fires once when a pair starts overlapping,
  `OnCollisionExit` fires once when a pair stops (including when either entity is destroyed
  between steps, which drops its contacts out of the "current" set with no final manifold to
  report), and `OnCollision` remains the per-step "stay" signal.
- **One-way platforms (Complete)** — `BoxCollider2D` carries `OneWay` (bool) and
  `SurfaceDirection` (unit `Vector2`, default up). `CollisionResolutionSystem` resolves a
  one-way contact only when the positional-correction push on the other body aligns with
  `SurfaceDirection` (contact on the solid side, not the underside) and that body's relative
  velocity isn't moving against `SurfaceDirection` (not still rising through it) — either
  condition failing skips resolution while still reporting the manifold via
  `OnCollision`/enter/exit, exactly like a trigger. `PhysicsWorld2D.DropThrough(entity, duration)`
  exempts an entity from one-way resolution for a window, for a down+jump drop-through input.
- **CharacterController2D: kinematic move-and-slide controller (Complete)** — `CharacterController2D`
  (box shape) + `CharacterControllerSystem` moves by axis-separated sweep-and-slide against solid
  `BoxCollider2D`/tilemap-generated obstacles instead of `CollisionResolutionSystem`'s impulse
  resolution: it integrates its own gravity (`GravityScale`, independent of `PhysicsWorld2D.Gravity`),
  resolves X then Y using a min-penetration-axis gate so an embedded spawn depenetrates upward
  rather than sideways and running across a seam between adjacent/merged colliders never snags,
  and zeroes velocity into a contact instead of bouncing. Exposes per-step `IsGrounded`,
  `IsTouchingWallLeft`/`IsTouchingWallRight`, `IsTouchingCeiling`, and `GroundNormal`; a small
  `ContactSkin` tolerance keeps those flags reporting a contact resolved to exactly zero overlap
  on later frames instead of dropping the moment velocity stops regenerating overlap. Respects
  layers/masks and one-way platforms (shared `OneWayPlatformFilter` helper, reused by
  `CollisionResolutionSystem`), and `StepHeight` auto-climbs short ledges.
- Joints and constraints (distance, hinge, spring) (Future)
- Continuous collision detection (CCD) to prevent tunneling at high velocities (Future)

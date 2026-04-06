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

### Phase 4: Advanced Features (Future)

- Joints and constraints (distance, hinge, spring)
- Continuous collision detection (CCD) to prevent tunneling at high velocities
- Collision layers and masks for filtering

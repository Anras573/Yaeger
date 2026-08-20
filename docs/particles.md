# Particle System

Yaeger's particle system emits, simulates, and renders large numbers of short-lived quads through the existing batched sprite renderer. Effects like fire, smoke, sparks, or explosions are configured with a single `ParticleEmitter` component instead of hand-managing hundreds of entities.

## Quick start

```csharp
var particleSystem = new ParticleSystem(world, renderer);

var fire = world.CreateEntity();
world.AddComponent(fire, new Transform2D(new Vector2(0f, -0.7f)));
world.AddComponent(fire, new ParticleEmitter("Assets/particle.png")
{
    MaxParticles = 512,
    EmitRate = 150f,                 // particles per second
    ParticleLifetime = 1.2f,         // seconds
    EmitDirection = new Vector2(0f, 1f),
    SpreadAngle = MathF.PI / 5f,     // total arc in radians
    InitialSpeed = 0.6f,
    StartColor = new Color(255, 200, 40),
    EndColor = new Color(255, 30, 0, 0),
    StartSize = 0.07f,
    EndSize = 0.015f,
});

window.OnUpdate += dt => particleSystem.Update((float)dt);
window.OnRender += _ =>
{
    renderSystem.Render();
    particleSystem.Render(); // after the main render pass — particles draw on top
};
```

See `Samples/ParticleDemo` for a complete program with a fire fountain, a smoke plume, and click-triggered explosions.

## The `ParticleEmitter` component

| Field | Type | Default | Meaning |
|-------|------|---------|---------|
| `MaxParticles` | `int` | `256` | Pool capacity; emission pauses while the pool is full |
| `EmitRate` | `float` | `50` | Particles per second (fractions carry over between frames) |
| `ParticleLifetime` | `float` | `1` | Seconds each particle lives |
| `EmitDirection` | `Vector2` | `(0, 1)` | Centre direction; only the angle matters |
| `SpreadAngle` | `float` | `π/4` | Total arc (radians) centred on `EmitDirection` |
| `InitialSpeed` | `float` | `1` | World units per second |
| `StartColor` / `EndColor` | `Color` | white | Tint lerped over each particle's lifetime |
| `StartSize` / `EndSize` | `float` | `0.1` | Quad size lerped over each particle's lifetime |
| `TexturePath` | `string` | (ctor) | Texture every particle is drawn with |

The emitter entity must also carry a `Transform2D`; its `Position` is where particles spawn.

For a radial burst (explosion), set `EmitDirection = Vector2.Zero` and `SpreadAngle = MathF.Tau`. For a one-shot effect, spawn an emitter entity, set `EmitRate` to `0` after a short burst window, and destroy the entity once its particles have died — `ParticleDemo` shows this pattern.

## Update vs Render

`ParticleSystem` implements `IUpdateSystem`:

- **`Update(deltaTime)`** — ages all live particles, recycles expired ones in-place, integrates velocity into position, and emits new particles at `EmitRate × deltaTime`. Call it from your update loop like any other system.
- **`Render()`** — submits one quad per live particle via `Renderer.SubmitQuad(...)` (colour and size lerped by normalized age) and flushes. Call it from `OnRender` **after** `UnifiedRenderSystem.Render()`; the main render pass begins the frame and would otherwise clear the particle submissions.

The split exists because simulation belongs to the update loop while quad submission must happen inside the render callback. Constructing `ParticleSystem` without a renderer is supported — `Update` still simulates and `Render` becomes a no-op, which is handy for tests and headless runs.

## Performance characteristics

- Each emitter owns a fixed-size `ParticlePool` — an array of `Particle` structs allocated once. Expired particles are recycled by swapping the last live particle into their slot, so **particle storage and recycling never allocate** after construction. (The ECS query enumerators used each frame are the same small allocation every system in the engine makes.)
- Particles flow through the renderer's existing batching: contiguous quads sharing a texture collapse into one draw call (up to 1 000 quads each). Give all emitters of an effect the same texture to keep batches large.
- Particles honour the active `Camera2D` (the renderer's view-projection applies to every quad).

## Known limitations

- No additive blend mode for 2D particles — fire/glow effects use regular alpha blending. (3D
  particles do have one — see below.)
- No gravity/acceleration, angular velocity, or texture animation on particles; velocity is constant for each particle's lifetime.
- `ParticleEmitter` has no prefab/scene serializer yet, so emitters are configured in code.

## 3D particles

`ParticleEmitter3D` + `Transform3D`, simulated by `ParticleSystem3D` and rendered by
`ParticleRenderSystem3D`, brings the same design to 3D scenes as camera-facing billboards: sparks,
muzzle flashes, embers, smoke, dust. Native-only (`ParticleRenderSystem3D` renders through
`Renderer3D`); `ParticleSystem3D` itself is platform-agnostic and runs headless like its 2D
counterpart.

### Quick start

```csharp
var particleSystem3D = new ParticleSystem3D(world);

var embers = world.CreateEntity();
world.AddComponent(embers, new Transform3D(new Vector3(0f, 0f, 0f), Quaternion.Identity, Vector3.One));
world.AddComponent(embers, new ParticleEmitter3D("Assets/particle.png")
{
    MaxParticles = 128,
    EmitRate = 20f,
    ParticleLifetime = 1.5f,
    EmitDirection = Vector3.UnitY,
    SpreadAngle = MathF.PI / 6f,
    InitialSpeed = 0.25f,
    StartColor = new Color(255, 160, 40, 220),
    EndColor = new Color(255, 60, 0, 0),
    StartSize = 0.05f,
    EndSize = 0.015f,
    BlendMode = MaterialBlendMode.Additive,
});

var particleRenderSystem3D = new ParticleRenderSystem3D(renderer3D, textures, world, window, particleSystem3D);

window.OnUpdate += dt => particleSystem3D.Update((float)dt);
window.OnRender += _ =>
{
    meshRenderSystem.Render();
    particleRenderSystem3D.Render(); // after the mesh passes — particles draw in their own pass
};
```

See `Samples/CornellBox` for a running example (an additive ember emitter and a velocity-stretched
spark emitter).

### The `ParticleEmitter3D` component

Mirrors `ParticleEmitter`'s fields (`MaxParticles`, `EmitRate`, `ParticleLifetime`, `SpreadAngle`,
`InitialSpeed`, `StartColor`/`EndColor`, `StartSize`/`EndSize`, `TexturePath`) with `EmitDirection`
as a `Vector3` (a 3D cone rather than a 2D arc — a spawned particle's direction deviates from
`EmitDirection` by up to `SpreadAngle / 2`, uniformly random around the axis) plus two 3D-specific
fields:

| Field | Type | Default | Meaning |
|-------|------|---------|---------|
| `BlendMode` | `MaterialBlendMode` | `Transparent` | `Transparent` (standard alpha blending, sorted back-to-front against other transparent emitters) or `Additive` (brightens the frame, order-independent — see issue #194). `Opaque`/`Cutout` are treated as `Transparent`. |
| `VelocityStretch` | `float` | `0` | World units of elongation per unit of speed, along the particle's direction of travel. `0` (default) keeps billboards square/round; a positive value stretches them into streaks — suited to sparks, bolts, and tracers. |
| `Shape` | `EmissionShape` | `Point` | Where a particle's starting position is sampled from — see below. |
| `DiscRadius` | `float` | `0` | Radius of the disc used by `Shape = Disc`. Ignored by `Point`; a non-positive value collapses `Disc` back to spawning at the centre. |
| `SpeedVariance` / `LifetimeVariance` / `SizeVariance` | `float` | `0` | Fractional jitter on `InitialSpeed`, `ParticleLifetime`, and a per-particle size multiplier — see below. |
| `RandomInitialRotation` | `bool` | `false` | Spawn each particle with a random billboard rotation instead of `0` — see below. |
| `Acceleration` | `Vector3` | `(0, 0, 0)` | Constant per-second-squared force applied every step — gravity, buoyancy, or wind bias — see below. |
| `Drag` | `float` | `0` | Exponential velocity damping per second — see below. |
| `Turbulence` / `TurbulenceFrequency` | `float` | `0` / `1` | Amplitude and frequency of a coherent noise perturbation applied to velocity — see below. |

### Emission shapes

`Shape = EmissionShape.Point` (the default) spawns every particle at the emitter's exact
`Transform3D.Position`, matching every emitter's behaviour before this feature existed.
`Shape = EmissionShape.Disc` instead spreads particles uniformly across a disc of `DiscRadius`,
oriented perpendicular to `EmitDirection` — what a brazier or torch wants: a fire filling a bowl
rather than a jet from a single point. The underlying sampling is `ParticleSystem3D.SampleDiscOffset`,
a public static (mirroring `RandomDirectionInCone`) so it's directly unit-testable.

### Per-particle jitter

`SpeedVariance`, `LifetimeVariance`, and `SizeVariance` each apply the same fractional-jitter shape
at spawn — `value * (1 + U(-variance, variance))`, via the public static `ParticleSystem3D.Jitter`
— so a plume isn't visually uniform: particles vary in how fast they leave, how long they live, and
how large they render (one multiplier per particle scales both `StartSize` and `EndSize` together,
preserving the size lerp's shape). All three default to `0` (no jitter), so an emitter that doesn't
set them renders identically to before.

`RandomInitialRotation` gives each particle a random starting billboard rotation instead of the
default `0`. It only shows through while a particle has no meaningful screen-space velocity to
project a rotation from instead (see `BillboardMath.ProjectVelocity`) — a moving or
`VelocityStretch`-stretched particle's rotation is still owned by its direction of travel, exactly
as before this field existed. Most visible on slow-moving or near-stationary particles.

All new randomness is drawn from the same seeded `Random` `ParticleSystem3D` already uses for cone
sampling, so a seeded run stays fully reproducible with every new feature in use.

### Forces: acceleration, drag, and turbulence

Particles integrate constant velocity by default — a spawned direction and speed, travelled in a
straight line until expiry. `Acceleration`, `Drag`, and `Turbulence` add a small force model on top,
applied inside `ParticlePool3D.Update` each step using **semi-implicit (symplectic) Euler**: this
step's forces update velocity first, and that *updated* velocity is what integrates position —
never the velocity from the start of the step. This is what keeps the integration stable under
constant acceleration (explicit Euler gains energy over time) and framerate-independent for a given
fixed step.

- **`Acceleration`** is a plain `Vector3` added to velocity every step, in world units per second
  squared — gravity (negative Y) for embers that arc back down, buoyancy (positive Y) for flame
  that rises and accelerates, or a horizontal bias for wind. One field covers all three.
- **`Drag`** scales velocity by `exp(-Drag * deltaTime)` every step — exponential decay toward
  zero, so a particle settles toward whatever terminal velocity `Acceleration` implies instead of
  coasting at spawn speed forever. Exponential decay never overshoots past zero the way a linear
  `1 - Drag * deltaTime` damping term would once the product exceeds 1.
- **`Turbulence`** adds a coherent noise displacement to velocity each step, scaled by
  `TurbulenceFrequency`, via `Yaeger.Graphics.ValueNoise3D` — a deterministic, continuous 3D value
  noise (hashed lattice values interpolated with a quintic fade curve, the same shape as classic
  Perlin noise). `ValueNoise3D.SampleFlow` samples the field at a particle's own position and age,
  offsetting the sample point by time so a stationary particle still perturbs instead of sampling a
  frozen value forever. No per-particle state is added for this — the field is a pure function of
  position and time already tracked by every particle, so `ParticlePool3D`'s fixed-size, no-allocation
  contract is unaffected.

All three default to zero, so an emitter that never touches them keeps its exact constant-velocity
motion.

### How billboards face the camera

`ParticleRenderSystem3D` extracts the rendering camera's world-space right/up axes from its view
matrix (`BillboardMath.ExtractCameraAxes`) once per frame, and `Renderer3D.DrawParticles` builds
every billboard quad from those two vectors — so a billboard faces the camera correctly from any
angle, including as the camera orbits. A velocity-stretched billboard additionally projects its
particle's velocity onto the camera's (right, up) plane (`BillboardMath.ProjectVelocity`) to find
the streak's length and rotation; a particle moving directly toward or away from the camera
projects to near-zero speed, so it correctly collapses back to a round/square billboard instead of
showing a spurious streak.

### Emitter draw order

Each frame, `ParticleRenderSystem3D` groups emitters by blend mode: `Transparent` emitters are
sorted back-to-front by their entity's `Transform3D.Position` via the same `TransparencySorter`
the 3D mesh transparent pass uses, then drawn; `Additive` emitters are drawn afterwards, in no
particular order (additive blending is order-independent). Both groups render inside one
`Renderer3D.BeginTransparentPass`/`EndTransparentPass` bracket — depth-tested against opaque scene
geometry but not depth-writing — after `MeshRenderSystem.Render()` has drawn the opaque and mesh
transparent passes.

### Performance characteristics

Same shape as the 2D system: each emitter owns a fixed-size `ParticlePool3D` that never allocates
after construction, and `ParticleRenderSystem3D` draws every live particle in one emitter through a
single `glDrawArraysInstanced` call — one draw call per emitter regardless of how many particles
are alive in it, visible via `Renderer3D.DrawCallCount`.

### Known limitations (3D)

- No prefab/scene serializer yet for `ParticleEmitter3D`, so emitters are configured in code.
- No continuous angular velocity (spin) or texture animation — same as the 2D system.
  `RandomInitialRotation` sets a rotation once at spawn; it doesn't animate afterward.
- Emission shape is point or disc only — no sphere/box/cone-surface volumes.
- `Acceleration`/`Drag`/`Turbulence` are per-particle forces only; there's no wind field, particle
  collision, or interaction between particles (e.g. one emitter's smoke isn't pushed by another's).
- Particles within one emitter aren't depth-sorted against each other (only whole emitters are
  sorted against each other), and additive emitters aren't sorted against transparent ones at all
  — acceptable for the order-independent glow/spark effects this is aimed at, but a dense cloud of
  overlapping alpha-blended (non-additive) particles from the *same* emitter can show sorting
  artifacts, the same class of limitation `docs/pbr.md`'s Transparency section documents for
  meshes.
- No GPU-side simulation, mesh particles, sub-emitters, particle collision, or soft
  (depth-faded) particles — see issue #195's scope.

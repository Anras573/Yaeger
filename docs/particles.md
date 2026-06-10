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

- No additive blend mode yet — fire/glow effects use regular alpha blending. An additive pass would need a separate flush and is a planned follow-up.
- No gravity/acceleration, angular velocity, or texture animation on particles; velocity is constant for each particle's lifetime.
- `ParticleEmitter` has no prefab/scene serializer yet, so emitters are configured in code.

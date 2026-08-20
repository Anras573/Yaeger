using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances every entity's <see cref="LightFlicker"/> and writes the sampled intensity onto
/// whichever of <see cref="PointLight"/>/<see cref="SpotLight"/> it carries (both, if both are
/// present), plus a small positional offset onto its <see cref="Transform3D"/> when
/// <see cref="LightFlicker.PositionJitter"/> is nonzero. Lives in <c>Yaeger.Core</c> — no
/// <c>Window</c>/GL dependency, same as <c>DayNightCycleSystem</c>; <c>MeshRenderSystem</c> re-reads
/// light components every frame, so no renderer changes are needed to see the flicker.
/// </summary>
/// <remarks>
/// Run this after whatever else positions the entity this frame (a parent-transform system, a
/// future path-follow system) and before any render system reads transforms/lights — the same
/// ordering convention <c>CameraFollowSystem</c> and <c>TransformHierarchySystem</c> document.
/// <para>
/// Jittering <see cref="Transform3D.Position"/> in place would conflict with anything else that
/// also writes the entity's position (a torch parented to a moving character): this system tracks
/// the offset it last applied per entity and subtracts it before computing a new one each update,
/// so it never accumulates and never fights another system's write from the same frame. Removing
/// <see cref="LightFlicker"/> (or destroying the entity) restores the light to
/// <see cref="LightFlicker.BaseIntensity"/> and undoes the last applied offset, rather than leaving
/// either at wherever the last sampled frame happened to land.
/// </para>
/// </remarks>
public class LightFlickerSystem : IUpdateSystem
{
    private readonly World _world;

    // Per-entity state this system owns outside the ECS (mirrors ParticleSystem3D's _pools):
    // enough to restore a light/transform to a sane value once its LightFlicker disappears,
    // and to undo the previous frame's position offset before applying a new one.
    private readonly Dictionary<Entity, FlickerState> _state = new();
    private readonly List<Entity> _expired = [];

    private struct FlickerState
    {
        public float BaseIntensity;
        public Vector3 PositionOffset;
    }

    public LightFlickerSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        // Guard against negative/non-finite deltas, same convention as DayNightCycleSystem.
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            return;

        var flickers = _world.GetStore<LightFlicker>();
        var pointLights = _world.GetStore<PointLight>();
        var spotLights = _world.GetStore<SpotLight>();
        var transforms = _world.GetStore<Transform3D>();

        foreach (var (entity, flicker) in flickers)
        {
            var updated = flicker;
            updated.Elapsed += deltaTime;

            var noise = LightFlickerSignal.Sample(updated.Elapsed, updated.Frequency, updated.Seed);
            var intensity = MathF.Max(updated.BaseIntensity + noise * updated.Amplitude, 0f);

            if (pointLights.TryGet(entity, out var point))
            {
                point.Intensity = intensity;
                pointLights.Add(entity, point);
            }

            if (spotLights.TryGet(entity, out var spot))
            {
                spot.Intensity = intensity;
                spotLights.Add(entity, spot);
            }

            var offset = Vector3.Zero;
            if (transforms.TryGet(entity, out var transform))
            {
                var previousOffset = _state.TryGetValue(entity, out var previous)
                    ? previous.PositionOffset
                    : Vector3.Zero;

                // Undo last frame's jitter before computing this frame's, so the offset never
                // accumulates and never permanently overwrites whatever else set the position
                // this frame.
                if (previousOffset != Vector3.Zero)
                    transform.Position -= previousOffset;

                offset = LightFlickerSignal.SampleOffset(
                    updated.Elapsed,
                    updated.Frequency,
                    updated.Seed,
                    updated.PositionJitter
                );

                if (offset != Vector3.Zero)
                    transform.Position += offset;

                if (previousOffset != Vector3.Zero || offset != Vector3.Zero)
                    transforms.Add(entity, transform);
            }

            _state[entity] = new FlickerState
            {
                BaseIntensity = updated.BaseIntensity,
                PositionOffset = offset,
            };
            flickers.Add(entity, updated);
        }

        RestoreExpired(flickers, pointLights, spotLights, transforms);
    }

    // Restores a light/transform to a sane value once its LightFlicker component (or entity) is
    // gone, rather than leaving it at whatever the last sampled frame left behind — mirrors
    // ParticleSystem3D.RemoveExpiredPools' diff-and-cleanup pattern.
    private void RestoreExpired(
        ComponentStorage<LightFlicker> flickers,
        ComponentStorage<PointLight> pointLights,
        ComponentStorage<SpotLight> spotLights,
        ComponentStorage<Transform3D> transforms
    )
    {
        foreach (var entity in _state.Keys)
        {
            if (!flickers.TryGet(entity, out _))
                _expired.Add(entity);
        }

        foreach (var entity in _expired)
        {
            var state = _state[entity];

            if (pointLights.TryGet(entity, out var point))
            {
                point.Intensity = state.BaseIntensity;
                pointLights.Add(entity, point);
            }

            if (spotLights.TryGet(entity, out var spot))
            {
                spot.Intensity = state.BaseIntensity;
                spotLights.Add(entity, spot);
            }

            if (
                state.PositionOffset != Vector3.Zero
                && transforms.TryGet(entity, out var transform)
            )
            {
                transform.Position -= state.PositionOffset;
                transforms.Add(entity, transform);
            }

            _state.Remove(entity);
        }

        _expired.Clear();
    }
}

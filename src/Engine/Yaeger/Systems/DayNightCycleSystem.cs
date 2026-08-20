using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances the scene's <see cref="TimeOfDay"/> and applies the lighting it evaluates to: the
/// <see cref="DirectionalLight"/> (sun or moon) and the <see cref="AmbientLight"/> are written back
/// onto the entity carrying the clock. Lives in <c>Yaeger.Core</c> — no <c>Window</c>/GL dependency,
/// same as <see cref="TweenSystem"/> and <see cref="TransformHierarchySystem"/>.
/// See docs/day-night.md.
/// </summary>
/// <remarks>
/// <para>
/// Run it before any render system, alongside the other gameplay updates. Nothing else is needed:
/// <c>MeshRenderSystem</c> re-reads the directional light and the ambient every frame, so the
/// shading and the shadow map follow the sun with no per-frame wiring of their own.
/// </para>
/// <para>
/// The cycle's entity should be the scene's only <see cref="DirectionalLight"/>. The renderer takes
/// the first one it finds, so an unrelated light entity created earlier would win and the cycle
/// would appear to do nothing.
/// </para>
/// <para>
/// Tone-map exposure is reported on <see cref="CurrentLighting"/> rather than applied — see
/// <see cref="DayNightLighting.Exposure"/> for why.
/// </para>
/// </remarks>
public class DayNightCycleSystem : IUpdateSystem
{
    private readonly World _world;

    /// <param name="world">The world queried for the <see cref="TimeOfDay"/> entity.</param>
    /// <param name="settings">
    /// Cycle tunables. Defaults to <see cref="DayNightCycleSettings.Default"/>.
    /// </param>
    public DayNightCycleSystem(World world, DayNightCycleSettings? settings = null)
    {
        _world = world;
        Settings = settings ?? DayNightCycleSettings.Default;
        CurrentLighting = DayNightCycle.Evaluate(TimeOfDay.Default, Settings);
    }

    /// <summary>
    /// The tunables the clock is evaluated against. Mutable so a game can art-direct the cycle at
    /// runtime (an overcast pass, a different season) without rebuilding the system.
    /// </summary>
    public DayNightCycleSettings Settings { get; set; }

    /// <summary>
    /// The lighting applied by the most recent <see cref="Update"/>. Seeded from
    /// <see cref="TimeOfDay.Default"/> at construction, and left untouched by an update that finds no
    /// <see cref="TimeOfDay"/> entity, so it always holds a usable value.
    /// </summary>
    public DayNightLighting CurrentLighting { get; private set; }

    /// <summary>
    /// Advances the clock by <paramref name="deltaTime"/> and applies the resulting lighting.
    /// A <paramref name="deltaTime"/> of zero applies the current time without advancing it, which
    /// is how a scrubbed or paused cycle is pushed to the scene.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Guard against negative/non-finite deltas, same convention as AnimationSystem/TweenSystem.
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            return;

        // First TimeOfDay in the world wins, matching MeshRenderSystem's "first light/camera/skybox"
        // convention — a scene has one clock.
        foreach ((Entity entity, TimeOfDay snapshot) in _world.GetStore<TimeOfDay>())
        {
            var time = snapshot;
            time.NormalizedTime = DayNightCycle.Wrap01(Advance(time, deltaTime));

            var lighting = DayNightCycle.Evaluate(time, Settings);
            CurrentLighting = lighting;

            _world.AddComponent(entity, time);
            _world.AddComponent(entity, lighting.KeyLight);
            _world.AddComponent(entity, lighting.Ambient);
            return;
        }
    }

    private static float Advance(in TimeOfDay time, float deltaTime)
    {
        var length = time.DayLengthSeconds;

        // A frozen cycle (zero/negative/non-finite length) still evaluates and applies its lighting;
        // it just doesn't move on its own, leaving NormalizedTime under the game's control.
        if (!float.IsFinite(length) || length <= 0f)
            return time.NormalizedTime;

        return time.NormalizedTime + deltaTime / length;
    }
}

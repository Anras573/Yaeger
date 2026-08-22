using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances every <see cref="ProceduralSky"/>'s <see cref="ProceduralSky.Elapsed"/> clock, which
/// drives the cloud layer's scroll offset. Lives in <c>Yaeger.Core</c> — no <c>Window</c>/GL
/// dependency, same as <c>DayNightCycleSystem</c>/<c>LightFlickerSystem</c>;
/// <c>MeshRenderSystem</c> re-reads the component every frame, so no renderer changes are needed to
/// see the drift.
/// </summary>
/// <remarks>
/// Run alongside the other gameplay updates, before rendering — order relative to
/// <c>DayNightCycleSystem</c> doesn't matter, since the two write disjoint fields
/// (<see cref="ProceduralSky.Elapsed"/> here; <see cref="ProceduralSky.SunDirection"/>/
/// <see cref="ProceduralSky.MoonDirection"/>/<see cref="ProceduralSky.DaylightFactor"/> there).
/// </remarks>
public class ProceduralSkySystem : IUpdateSystem
{
    private readonly World _world;

    public ProceduralSkySystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        // Guard against negative/non-finite deltas, same convention as DayNightCycleSystem/LightFlickerSystem.
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            return;

        var skies = _world.GetStore<ProceduralSky>();
        foreach (var (entity, sky) in skies)
        {
            var updated = sky;
            updated.Elapsed += deltaTime;
            skies.Add(entity, updated);
        }
    }
}

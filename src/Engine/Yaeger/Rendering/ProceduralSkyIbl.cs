using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Drives image-based lighting from a <see cref="ProceduralSky"/> instead of a static
/// <see cref="Skybox"/> cubemap: bakes the current sky into a cubemap (<see cref="ProceduralSkyRenderer.Bake"/>)
/// and prefilters it (<see cref="IblPrefilter.Prefilter(uint, int, int)"/>) on a throttle, so the
/// scene's ambient and reflections track a moving sun without re-running the full prefilter chain
/// every frame. This is the dynamic counterpart to <see cref="EnvironmentMapRegistry"/>, which
/// prefilters a <see cref="Skybox"/> once at registration and never again — a scene with a static
/// six-image skybox should keep using that instead and pays nothing extra by this type existing.
/// </summary>
/// <remarks>
/// Call <see cref="Update"/> once per frame — typically from the same <c>OnUpdate</c> callback that
/// advances <c>ProceduralSkySystem</c>/<c>DayNightCycleSystem</c> — passing the current
/// <see cref="ProceduralSky"/> value and frame delta. Wire the instance into <c>MeshRenderSystem</c>'s
/// <c>proceduralSkyIbl</c> constructor parameter for the read side; <c>MeshRenderSystem</c> only ever
/// calls <see cref="TryGet"/>, never <see cref="Update"/> — driving the bake from the render loop
/// would tie its cadence to frame rate rather than to <see cref="ProceduralSkyIblSettings.MinRebakeInterval"/>,
/// the same reason <see cref="EnvironmentMapRegistry.Register"/> is caller-driven rather than
/// automatic.
/// </remarks>
public sealed class ProceduralSkyIbl(ProceduralSkyRenderer skyRenderer, IblPrefilter prefilter)
    : IDisposable
{
    /// <summary>
    /// Throttle tunables. Mutable so a game can retune the bake cadence at runtime (e.g. loosen it
    /// during a fast-forwarded cycle), the same way <c>DayNightCycleSystem.Settings</c> is.
    /// </summary>
    public ProceduralSkyIblSettings Settings { get; set; } = ProceduralSkyIblSettings.Default;

    /// <summary>The most recently baked environment map, or <c>null</c> before the first <see cref="Update"/>.</summary>
    public EnvironmentMap? Current { get; private set; }

    private Vector3 _lastBakedSunDirection;
    private float _elapsedSinceLastBake;

    /// <summary>
    /// Re-bakes and re-prefilters <paramref name="sky"/> if <see cref="ShouldRebake"/> says it's
    /// due — always on the first call, and thereafter only once both
    /// <see cref="ProceduralSkyIblSettings.MinRebakeInterval"/> has elapsed and the sun has moved
    /// past <see cref="ProceduralSkyIblSettings.SunDirectionThresholdDegrees"/> since the last bake.
    /// A no-op call (the common case) costs one vector comparison — no GL work.
    /// </summary>
    /// <param name="sky">The current sky to (possibly) bake.</param>
    /// <param name="deltaTime">Seconds since the last <see cref="Update"/> call. Non-finite or
    /// negative values are ignored (contribute no elapsed time) rather than corrupting the throttle,
    /// the same guard <c>DayNightCycleSystem.Update</c> applies to its own delta.</param>
    /// <param name="viewportWidth">Current window viewport width, restored after the bake/prefilter
    /// passes if a re-bake happens this call.</param>
    /// <param name="viewportHeight">Current window viewport height, restored the same way.</param>
    public void Update(ProceduralSky sky, float deltaTime, int viewportWidth, int viewportHeight)
    {
        if (float.IsFinite(deltaTime) && deltaTime > 0f)
            _elapsedSinceLastBake += deltaTime;

        var hasBaked = Current != null;
        var angleSinceLastBake = hasBaked
            ? AngleBetweenDegrees(sky.SunDirection, _lastBakedSunDirection)
            : 0f;

        if (!ShouldRebake(hasBaked, _elapsedSinceLastBake, angleSinceLastBake, Settings))
            return;

        var cubemapHandle = skyRenderer.Bake(sky, viewportWidth, viewportHeight);
        var newMap = prefilter.Prefilter(cubemapHandle, viewportWidth, viewportHeight);

        Current?.Dispose();
        Current = newMap;
        _lastBakedSunDirection = sky.SunDirection;
        _elapsedSinceLastBake = 0f;
    }

    /// <summary>Looks up the most recently baked environment map, if any.</summary>
    public bool TryGet(out EnvironmentMap? environmentMap)
    {
        environmentMap = Current;
        return Current != null;
    }

    /// <summary>
    /// Pure re-bake decision: always due without a previous bake (<paramref name="hasBaked"/>
    /// false); otherwise due only once <paramref name="elapsedSinceLastBake"/> has reached
    /// <see cref="ProceduralSkyIblSettings.MinRebakeInterval"/> <i>and</i>
    /// <paramref name="angleSinceLastBakeDegrees"/> has reached
    /// <see cref="ProceduralSkyIblSettings.SunDirectionThresholdDegrees"/>.
    /// </summary>
    /// <remarks>
    /// The interval is an AND-gated floor, not an alternate trigger: a sun that moves fast (a short
    /// <c>DayLengthSeconds</c>) would otherwise cross a small angle threshold many times within one
    /// interval window, defeating the point of a floor. Requiring both is what keeps a long-running,
    /// fast-forwarded cycle to a bounded number of prefilter runs regardless of how quickly the sun
    /// moves — the interval alone sets the ceiling, and the angle check only skips a bake once that
    /// ceiling allows one but the sky hasn't actually changed enough to be worth it (e.g. a paused or
    /// very slow cycle).
    /// </remarks>
    public static bool ShouldRebake(
        bool hasBaked,
        float elapsedSinceLastBake,
        float angleSinceLastBakeDegrees,
        ProceduralSkyIblSettings settings
    )
    {
        if (!hasBaked)
            return true;

        var minInterval = MathF.Max(Finite(settings.MinRebakeInterval), 0f);
        var threshold = MathF.Max(Finite(settings.SunDirectionThresholdDegrees), 0f);

        return Finite(elapsedSinceLastBake) >= minInterval
            && Finite(angleSinceLastBakeDegrees) >= threshold;
    }

    /// <summary>
    /// The angle, in degrees, between two directions — used to measure how far the sun has moved
    /// since the last bake. Non-finite or zero-length inputs contribute no angle (treated as
    /// unchanged) rather than throwing or returning NaN.
    /// </summary>
    public static float AngleBetweenDegrees(Vector3 a, Vector3 b)
    {
        var lengthA = a.Length();
        var lengthB = b.Length();
        if (!float.IsFinite(lengthA) || !float.IsFinite(lengthB) || lengthA <= 0f || lengthB <= 0f)
            return 0f;

        var cosAngle = Vector3.Dot(a, b) / (lengthA * lengthB);
        if (!float.IsFinite(cosAngle))
            return 0f;

        return MathF.Acos(Math.Clamp(cosAngle, -1f, 1f)) * (180f / MathF.PI);
    }

    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;

    /// <summary>Disposes the most recently baked environment map, if any. Does not dispose the injected <see cref="ProceduralSkyRenderer"/>/<see cref="IblPrefilter"/> — they're owned by the caller.</summary>
    public void Dispose() => Current?.Dispose();
}

using System.Numerics;

namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side math for <see cref="ProceduralSkyRenderer"/> — no GL calls, so (like
/// <see cref="BillboardMath"/> and <see cref="MeshInstanceBatcher"/>) this is unit-testable without a
/// live OpenGL context. Computed once per draw and uploaded as uniforms, rather than recomputed
/// per-fragment in the shader, which is what makes both pieces directly testable outside it.
/// </summary>
public static class ProceduralSkyMath
{
    /// <summary>
    /// The rotation applied to a sampled direction before hashing it into the star field, so the
    /// whole field appears to wheel overhead as the night goes on rather than sitting fixed relative
    /// to the world.
    /// </summary>
    /// <remarks>
    /// Reuses the sun's own horizontal-plane angle — the same <c>atan2</c> that recovers
    /// <c>DayNightCycle.SunDirection</c>'s construction angle from its X/Z components — as a
    /// monotonic, once-per-day phase. It isn't real sidereal motion (a day and a star's apparent
    /// revolution aren't the same length in reality), but a game doesn't need that: it only needs a
    /// rotation that completes one full turn per day/night cycle and holds still when the sun does
    /// (a frozen <see cref="Yaeger.Graphics.TimeOfDay"/> gives a still sky, matching every other
    /// day/night-driven visual).
    /// </remarks>
    public static Matrix4x4 StarRotation(Vector3 sunDirection)
    {
        var angle = MathF.Atan2(Finite(sunDirection.Z), Finite(sunDirection.X));
        return Matrix4x4.CreateRotationY(angle);
    }

    /// <summary>
    /// The cloud layer's scroll offset at <paramref name="elapsed"/> seconds: simply
    /// <c>wind * elapsed</c>, but centralised here so it's exercised by the same tests as
    /// <see cref="StarRotation"/> rather than inlined at the call site. Non-finite inputs (a stray
    /// NaN wind component, an unbounded <paramref name="elapsed"/> after a very long session) sanitize
    /// to zero contribution on that axis instead of poisoning the whole offset.
    /// </summary>
    public static Vector2 CloudScrollOffset(Vector2 wind, float elapsed)
    {
        var time = Finite(elapsed);
        return new Vector2(Finite(wind.X) * time, Finite(wind.Y) * time);
    }

    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}

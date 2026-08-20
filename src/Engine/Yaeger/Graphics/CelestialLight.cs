namespace Yaeger.Graphics;

/// <summary>Which body in a day/night cycle a <see cref="CelestialLight"/> stands for.</summary>
public enum CelestialBody
{
    Sun,
    Moon,
}

/// <summary>
/// Marks an entity as the sun or the moon of a day/night cycle. <c>DayNightCycleSystem</c> writes
/// that body's evaluated <see cref="DirectionalLight"/> onto every entity carrying one, so a scene
/// that wants both lit at dusk creates two entities and tags them.
/// </summary>
/// <remarks>
/// <para>
/// Optional. A scene with no <see cref="CelestialLight"/> entity keeps the single-key-light
/// behaviour: the cycle writes whichever body is above the horizon onto its own
/// <see cref="TimeOfDay"/> entity. Tagging is the opt-in for lighting with both at once, which
/// <c>Renderer3D</c> supports up to <c>Renderer3D.MaxDirectionalLights</c>.
/// </para>
/// <para>
/// Tagging more than one entity as the same body is allowed — each gets the same light — but the
/// renderer only accumulates the first <c>MaxDirectionalLights</c> directional lights it finds, so
/// the extras are silently dropped.
/// </para>
/// </remarks>
public record struct CelestialLight(CelestialBody Body);

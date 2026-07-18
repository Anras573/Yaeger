using System.Numerics;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Shared geometric test for one-way ("jump-through") platform contacts, used by both
/// <see cref="CollisionResolutionSystem"/> (impulse-resolved bodies) and
/// <see cref="CharacterControllerSystem"/> (kinematic move-and-slide), so the two movement
/// paths agree on exactly which contacts pass through a one-way platform.
/// </summary>
internal static class OneWayPlatformFilter
{
    /// <summary>
    /// A contact with a one-way platform should pass through unless both hold: the direction
    /// the other body would be pushed aligns with the platform's <paramref name="surfaceDirection"/>
    /// (the contact is on the solid side, not the underside or an edge), and the other body's
    /// velocity relative to the platform isn't moving against that direction (i.e. not still
    /// rising up through it).
    /// </summary>
    /// <param name="pushOnOther">
    /// The direction the non-platform body would be moved to resolve the overlap.
    /// </param>
    /// <param name="surfaceDirection">The one-way platform's solid-side unit direction.</param>
    /// <param name="relativeVelocity">The other body's velocity relative to the platform.</param>
    public static bool ShouldPassThrough(
        Vector2 pushOnOther,
        Vector2 surfaceDirection,
        Vector2 relativeVelocity
    )
    {
        if (Vector2.Dot(pushOnOther, surfaceDirection) <= 0f)
            return true;

        return Vector2.Dot(relativeVelocity, surfaceDirection) > 0f;
    }
}

using System.Numerics;

namespace Yaeger.Audio;

/// <summary>
/// Pure CPU-side math backing <see cref="Yaeger.Systems.AudioSystem"/>'s listener sync: deriving
/// an OpenAL listener's at/up orientation from a camera's view matrix, and mapping a 2D position
/// onto the plane used for 2D positional audio. Kept separate from
/// <see cref="Yaeger.Systems.AudioSystem"/> (which also drives the live OpenAL context) so this
/// half can be unit-tested without a real audio device — the same split
/// <c>MeshInstanceBatcher</c>/<c>PostProcessPlanner</c> use for their systems.
/// </summary>
public static class AudioSpatialMath
{
    /// <summary>
    /// The fixed listener/source orientation used for 2D positional audio: facing into the
    /// screen (-Z, matching the engine's 2D rendering convention) with +Y as up. Both the
    /// listener and every 2D source live on the Z=0 plane (see <see cref="ToListenerPlane"/>),
    /// so this orientation is constant rather than derived per-frame.
    /// </summary>
    public static readonly (Vector3 At, Vector3 Up) PlaneOrientation = (
        -Vector3.UnitZ,
        Vector3.UnitY
    );

    /// <summary>
    /// Maps a 2D world position onto the Z=0 plane used for 2D positional audio — both sources
    /// and the listener are placed on this plane, so panning/attenuation is driven entirely by
    /// their XY offset.
    /// </summary>
    public static Vector3 ToListenerPlane(Vector2 position) => new(position.X, position.Y, 0f);

    /// <summary>
    /// Extracts the world-space forward ("at") and up vectors a <paramref name="viewMatrix"/>
    /// (as produced by <see cref="Yaeger.Graphics.Camera3D.ViewMatrix"/>, i.e.
    /// <see cref="Matrix4x4.CreateLookAt"/>) encodes, for driving an OpenAL listener's
    /// orientation.
    /// </summary>
    /// <remarks>
    /// <see cref="Matrix4x4.CreateLookAt"/> packs the camera's world-space right/up/backward
    /// basis vectors into the matrix's rows — right in (M11, M21, M31), up in (M12, M22, M32),
    /// backward in (M13, M23, M33) — with "at" being the negated backward vector. A degenerate
    /// matrix (e.g. <see cref="Yaeger.Graphics.Camera3D.ViewMatrix"/>'s <c>Matrix4x4.Identity</c>
    /// fallback for a zero-length look direction) falls back to <see cref="PlaneOrientation"/>.
    /// </remarks>
    public static (Vector3 At, Vector3 Up) ExtractOrientation(Matrix4x4 viewMatrix)
    {
        var backward = new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33);
        var up = new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32);

        if (backward.LengthSquared() < 1e-12f || up.LengthSquared() < 1e-12f)
            return PlaneOrientation;

        return (-Vector3.Normalize(backward), Vector3.Normalize(up));
    }
}

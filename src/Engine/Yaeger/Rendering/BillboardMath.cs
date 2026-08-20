using System.Numerics;

namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side math for camera-facing particle billboards — no GL calls, so (like
/// <see cref="TransparencySorter"/>, <see cref="CameraFrustum"/>, and
/// <see cref="MeshInstanceBatcher"/>) this is unit-testable without a live OpenGL context.
/// </summary>
public static class BillboardMath
{
    /// <summary>
    /// Extracts the world-space right and up axes of the camera that produced <paramref name="view"/>
    /// (a <see cref="Matrix4x4.CreateLookAt"/>-shaped matrix, as returned by <c>Camera3D.ViewMatrix</c>).
    /// A billboard quad built from these two axes always faces the camera, regardless of the
    /// camera's own orientation. Falls back to the world +X/+Y axes if a row degenerates to zero
    /// (a non-invertible/degenerate view matrix, which a valid camera never produces).
    /// </summary>
    public static (Vector3 Right, Vector3 Up) ExtractCameraAxes(Matrix4x4 view)
    {
        // CreateLookAt lays the camera's world-space right/up/back axes down its first three
        // *columns* (M11,M21,M31 / M12,M22,M32 / M13,M23,M33) — reading down a column recovers the
        // corresponding world-space axis directly, with no matrix inversion needed.
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);

        return (Normalize(right, Vector3.UnitX), Normalize(up, Vector3.UnitY));
    }

    private static Vector3 Normalize(Vector3 v, Vector3 fallback) =>
        v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : fallback;

    /// <summary>
    /// Projects <paramref name="velocity"/> onto the camera's (<paramref name="cameraRight"/>,
    /// <paramref name="cameraUp"/>) plane and returns the projected speed (used to elongate a
    /// velocity-stretched billboard) and the angle, in radians, to rotate the billboard's local
    /// +X axis onto that projected direction. A particle moving straight toward/away from the
    /// camera projects to (near) zero speed — correctly collapsing a stretched billboard back to
    /// its round/square base size instead of showing a spurious streak — in which case the
    /// rotation is 0 (arbitrary, since the billboard is square at that point anyway).
    /// </summary>
    public static (float ProjectedSpeed, float Rotation) ProjectVelocity(
        Vector3 velocity,
        Vector3 cameraRight,
        Vector3 cameraUp
    )
    {
        var x = Vector3.Dot(velocity, cameraRight);
        var y = Vector3.Dot(velocity, cameraUp);
        var projectedSpeed = MathF.Sqrt(x * x + y * y);

        return projectedSpeed > 1e-6f ? (projectedSpeed, MathF.Atan2(y, x)) : (0f, 0f);
    }

    /// <summary>
    /// Computes a particle's current flipbook frame from its spawn frame and age: it advances at
    /// <paramref name="frameRate"/> frames per second and loops across the full
    /// <paramref name="totalFrames"/> grid for the rest of the particle's life, rather than holding
    /// the last frame — see <see cref="Yaeger.Graphics.ParticleEmitter3D.FrameRate"/>. A non-positive
    /// <paramref name="frameRate"/> (or a non-finite <paramref name="age"/>) holds at
    /// <paramref name="startFrame"/>.
    /// </summary>
    public static int ComputeFrameIndex(int startFrame, float age, float frameRate, int totalFrames)
    {
        var frames = Math.Max(totalFrames, 1);
        if (frameRate <= 0f || !float.IsFinite(age))
            return Mod(startFrame, frames);

        var advanced = (int)MathF.Floor(MathF.Max(age, 0f) * frameRate);
        return Mod(startFrame + advanced, frames);
    }

    // C#'s % can return a negative result for a negative left operand; wrap into [0, modulus).
    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    /// <summary>
    /// Returns the normalised UV rectangle for <paramref name="frameIndex"/> in a
    /// <paramref name="columns"/> x <paramref name="rows"/> grid — frames indexed left-to-right,
    /// top-to-bottom, mirroring <see cref="Yaeger.Graphics.SpriteSheet.GetFrameUv"/>'s math exactly.
    /// Unlike that method, out-of-range inputs are clamped rather than thrown: this runs every
    /// frame inside a live render loop against emitter/particle state rather than once against
    /// author-supplied prefab data.
    /// </summary>
    public static (Vector2 UvMin, Vector2 UvMax) GetFrameUv(int columns, int rows, int frameIndex)
    {
        var cols = Math.Max(columns, 1);
        var rowCount = Math.Max(rows, 1);
        var frame = Math.Clamp(frameIndex, 0, cols * rowCount - 1);

        var col = frame % cols;
        var row = frame / cols;

        var frameWidth = 1f / cols;
        var frameHeight = 1f / rowCount;

        var uMin = col * frameWidth;
        var uMax = uMin + frameWidth;
        var vMax = 1f - row * frameHeight;
        var vMin = vMax - frameHeight;

        return (new Vector2(uMin, vMin), new Vector2(uMax, vMax));
    }

    /// <summary>
    /// Converts a non-linear perspective device depth in <c>[0, 1]</c> (as sampled straight from a
    /// depth texture, or read from <c>gl_FragCoord.z</c>) to linear view-space depth — positive
    /// distance from the camera, in world units. The shader-side half of soft particles
    /// (<see cref="FadeFactor"/>) does the same conversion; this is the CPU-testable mirror of it.
    /// </summary>
    public static float LinearizeDepth(float deviceDepth, float near, float far)
    {
        var n = near > 0f ? near : 0.0001f;
        var f = far > n ? far : n + 1f;
        var ndc = deviceDepth * 2f - 1f;

        var denominator = f + n - ndc * (f - n);
        return MathF.Abs(denominator) > 1e-8f ? (2f * n * f) / denominator : f;
    }

    /// <summary>
    /// Soft-particle alpha multiplier in <c>[0, 1]</c>: 1 (fully visible) when the scene surface
    /// behind a particle is at least <paramref name="fadeDistance"/> world units farther away than
    /// the particle itself, fading linearly to 0 (fully transparent) as that gap closes to zero —
    /// the standard technique for hiding a billboard's hard intersection edge with geometry behind
    /// it. A non-positive <paramref name="fadeDistance"/> disables the fade entirely (always 1),
    /// matching <see cref="Yaeger.Graphics.ParticleEmitter3D.SoftFade"/>'s "0 = hard cutoff" default.
    /// </summary>
    public static float FadeFactor(float sceneDepth, float particleDepth, float fadeDistance)
    {
        if (fadeDistance <= 0f)
            return 1f;

        return Math.Clamp((sceneDepth - particleDepth) / fadeDistance, 0f, 1f);
    }
}

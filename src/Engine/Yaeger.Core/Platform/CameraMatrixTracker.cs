using System.Numerics;

namespace Yaeger.Platform;

/// <summary>
/// Remembers the view-projection a quad batch surface is currently using, so
/// <c>SetCamera</c> can tell when queued quads must be flushed (with the old matrix)
/// before the new one takes effect. Shared by the native and browser surfaces.
/// </summary>
public sealed class CameraMatrixTracker
{
    /// <summary>The matrix last accepted. Starts as <see cref="Matrix4x4.Identity"/>.</summary>
    public Matrix4x4 Current { get; private set; } = Matrix4x4.Identity;

    /// <summary>
    /// Switches to <paramref name="next"/>. If it differs from <see cref="Current"/>,
    /// <paramref name="flushPending"/> runs first — while <see cref="Current"/> is still
    /// the old matrix — so quads queued so far are drawn with the matrix they were
    /// submitted under. Returns whether the matrix changed.
    /// </summary>
    public bool Set(Matrix4x4 next, Action flushPending)
    {
        if (next.Equals(Current))
            return false;
        flushPending();
        Current = next;
        return true;
    }
}

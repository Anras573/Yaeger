using System.Numerics;

namespace Yaeger.Platform;

/// <summary>
/// Describes the drawable area a 2D render or camera system targets. Lets platform-agnostic
/// systems derive an aspect ratio without depending on a concrete window or canvas.
/// </summary>
public interface IViewport
{
    /// <summary>Logical (CSS / window) size in pixels.</summary>
    Vector2 Size { get; }

    /// <summary>
    /// Physical-to-logical pixel ratio (<c>devicePixelRatio</c> on the web,
    /// framebuffer/window ratio on desktop).
    /// </summary>
    float PixelRatio { get; }

    /// <summary>Width over height of <see cref="Size"/>; <c>1</c> when the height is not positive.</summary>
    float AspectRatio => Size.Y > 0 ? Size.X / Size.Y : 1f;
}

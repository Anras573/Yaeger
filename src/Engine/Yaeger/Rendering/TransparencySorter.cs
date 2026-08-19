using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side bookkeeping for <see cref="Systems.MeshRenderSystem"/>'s transparent draw pass:
/// classifying a <see cref="Material3D"/> into the opaque/cutout main pass versus the transparent
/// pass, and computing/applying the view-space depth key the transparent pass sorts back-to-front
/// by. No GL calls, so — like <see cref="MeshInstanceBatcher"/> and <see cref="CameraFrustum"/> —
/// this is unit-testable without a live OpenGL context.
/// </summary>
public static class TransparencySorter
{
    /// <summary>
    /// True when <paramref name="material"/> belongs in the sorted blended pass (drawn after every
    /// opaque/cutout material, sorted back-to-front, depth-tested but not depth-written) — that is,
    /// <see cref="MaterialBlendMode.Transparent"/> or <see cref="MaterialBlendMode.Additive"/>.
    /// Additive shares this pass rather than getting its own: it's order-independent among other
    /// additive surfaces, but its position relative to alpha-blended surfaces still matters. Opaque
    /// and cutout materials both draw in the main pass — cutout's alpha test needs no sorting.
    /// </summary>
    public static bool IsTransparent(Material3D material) =>
        material.BlendMode is MaterialBlendMode.Transparent or MaterialBlendMode.Additive;

    /// <summary>
    /// View-space depth of a point, used as the transparent pass's back-to-front sort key: larger
    /// values are farther from the camera. In view space (camera looking down -Z) that distance is
    /// <c>-viewSpace.Z</c>; a <paramref name="view"/> that isn't a valid rigid/affine view transform
    /// (e.g. the identity fallback used when a scene has no camera) still produces a well-defined,
    /// if not physically meaningful, ordering.
    /// </summary>
    public static float ViewDepth(Vector3 worldPosition, Matrix4x4 view) =>
        -Vector3.Transform(worldPosition, view).Z;

    /// <summary>
    /// Sorts <paramref name="entries"/> in place, farthest-from-camera first, by the view-space
    /// depth of each entry's <paramref name="positionSelector"/> result. Stable-ish in practice
    /// (List&lt;T&gt;.Sort is not a stable sort, but ties are rare for distinct world positions) —
    /// per-triangle/within-mesh ordering is explicitly out of scope; this is object-level only.
    /// </summary>
    public static void SortBackToFront<T>(
        List<T> entries,
        Matrix4x4 view,
        Func<T, Vector3> positionSelector
    )
    {
        entries.Sort(
            (a, b) =>
                ViewDepth(positionSelector(b), view).CompareTo(ViewDepth(positionSelector(a), view))
        );
    }
}

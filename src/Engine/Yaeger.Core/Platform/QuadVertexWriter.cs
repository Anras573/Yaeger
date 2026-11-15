using System.Numerics;
using System.Runtime.CompilerServices;

namespace Yaeger.Platform;

/// <summary>
/// Writes one quad's four vertices (position, UV, colour — nine floats each) into a vertex
/// buffer. Shared by the desktop <c>Renderer</c> and <c>BrowserRenderSurface</c> so both fill
/// their buffers identically.
/// </summary>
/// <remarks>
/// A quad is described by its centre and the two edge vectors of the unit quad after
/// transformation (<c>xAxis</c> = the image of the unit X step, <c>yAxis</c> = the image of the
/// unit Y step) — i.e. the translation and basis rows of an affine transform. This replaces four
/// general <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> calls with a handful of adds.
/// Corner order matches the winding baked into the index buffer: TR, BR, BL, TL.
/// </remarks>
public static class QuadVertexWriter
{
    /// <summary>Floats per vertex: 3 position + 2 texcoord + 4 colour.</summary>
    public const int FloatsPerVertex = 9;

    /// <summary>Floats per quad (four vertices).</summary>
    public const int FloatsPerQuad = 4 * FloatsPerVertex;

    /// <summary>
    /// Extracts the centre and edge vectors from <paramref name="transform"/>, which maps the
    /// unit quad (centred on the origin, spanning -0.5..0.5) into the world.
    /// </summary>
    public static void FromMatrix(
        in Matrix4x4 transform,
        out Vector3 origin,
        out Vector3 xAxis,
        out Vector3 yAxis
    )
    {
        origin = new Vector3(transform.M41, transform.M42, transform.M43);
        xAxis = new Vector3(transform.M11, transform.M12, transform.M13);
        yAxis = new Vector3(transform.M21, transform.M22, transform.M23);
    }

    /// <summary>
    /// Writes the four vertices of a quad starting at <c>destination[0]</c>
    /// (<see cref="FloatsPerQuad"/> floats).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(
        Span<float> destination,
        in Vector3 origin,
        in Vector3 xAxis,
        in Vector3 yAxis,
        in Vector2 uvMin,
        in Vector2 uvMax,
        in Vector4 color
    )
    {
        var hx = xAxis * 0.5f;
        var hy = yAxis * 0.5f;

        // TR, BR, BL, TL
        WriteVertex(destination[0..], origin + hx + hy, uvMax.X, uvMax.Y, color);
        WriteVertex(destination[FloatsPerVertex..], origin + hx - hy, uvMax.X, uvMin.Y, color);
        WriteVertex(
            destination[(2 * FloatsPerVertex)..],
            origin - hx - hy,
            uvMin.X,
            uvMin.Y,
            color
        );
        WriteVertex(
            destination[(3 * FloatsPerVertex)..],
            origin - hx + hy,
            uvMin.X,
            uvMax.Y,
            color
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteVertex(
        Span<float> v,
        in Vector3 position,
        float u,
        float t,
        in Vector4 color
    )
    {
        v[8] = color.W; // one bounds check for the whole vertex
        v[0] = position.X;
        v[1] = position.Y;
        v[2] = position.Z;
        v[3] = u;
        v[4] = t;
        v[5] = color.X;
        v[6] = color.Y;
        v[7] = color.Z;
    }
}

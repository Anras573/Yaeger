using System.Numerics;
using System.Runtime.InteropServices;

namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side packing of multiple skinning matrix palettes into one flat texel buffer for
/// <see cref="Renderer3D.DrawInstancedSkinned"/>'s bone-palette texture buffer (a <c>samplerBuffer</c>
/// sampled with <c>texelFetch</c> in the vertex shader, instead of the fixed-size UBO the immediate
/// skinned draw path uses). Each bone matrix occupies <see cref="TexelsPerBone"/> consecutive
/// <see cref="Vector4"/> texels — its raw column-major layout, identical to how
/// <see cref="Renderer3D.SetBoneMatrices"/> already copies a <see cref="Matrix4x4"/> span's bytes
/// straight into the UBO. No GL calls — unit-testable the same way <see cref="MeshInstanceBatcher"/>
/// and <c>PostProcessPlanner</c> are.
/// </summary>
public static class BonePaletteBuffer
{
    /// <summary>Texels (<see cref="Vector4"/>s) one bone matrix occupies: its four columns.</summary>
    public const int TexelsPerBone = 4;

    /// <summary>
    /// Total texel count <see cref="Pack"/> would write for
    /// <paramref name="palettes"/>[<paramref name="start"/>..<paramref name="start"/>+<paramref name="count"/>).
    /// </summary>
    public static int TotalTexels(IReadOnlyList<Matrix4x4[]> palettes, int start, int count)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
            total += palettes[start + i].Length * TexelsPerBone;
        return total;
    }

    /// <summary>Total texel count <see cref="Pack"/> would write for every entry in <paramref name="palettes"/>.</summary>
    public static int TotalTexels(IReadOnlyList<Matrix4x4[]> palettes) =>
        TotalTexels(palettes, 0, palettes.Count);

    /// <summary>
    /// Writes <paramref name="palettes"/>[<paramref name="start"/>..<paramref name="start"/>+<paramref name="count"/>)
    /// into <paramref name="destination"/> back-to-back, each palette's matrices in order. Returns
    /// the number of texels written (matches <see cref="TotalTexels(IReadOnlyList{Matrix4x4[]}, int, int)"/>
    /// for the same range). <paramref name="destination"/> must be at least that long.
    /// </summary>
    public static int Pack(
        IReadOnlyList<Matrix4x4[]> palettes,
        int start,
        int count,
        Span<Vector4> destination
    )
    {
        var cursor = 0;
        for (var i = 0; i < count; i++)
        {
            var texels = MemoryMarshal.Cast<Matrix4x4, Vector4>(palettes[start + i]);
            texels.CopyTo(destination[cursor..]);
            cursor += texels.Length;
        }
        return cursor;
    }

    /// <summary>Packs every entry in <paramref name="palettes"/>. Equivalent to <see cref="Pack(IReadOnlyList{Matrix4x4[]}, int, int, Span{Vector4})"/> over the full range.</summary>
    public static int Pack(IReadOnlyList<Matrix4x4[]> palettes, Span<Vector4> destination) =>
        Pack(palettes, 0, palettes.Count, destination);
}

namespace Yaeger.Rendering;

/// <summary>
/// Splits a group of per-instance skinning palettes into contiguous chunks that each fit within the
/// bone-palette texture buffer's texel cap, so <see cref="Renderer3D.DrawInstancedSkinned"/> never
/// drops instances silently when a group's total palette size would exceed
/// <see cref="MaxPaletteTexels"/> — it issues one instanced draw call per chunk instead. Pure C#, no
/// GL calls — unit-tested the same way <see cref="MeshInstanceBatcher"/> is.
/// </summary>
public static class InstancedSkinningPlanner
{
    /// <summary>
    /// Texel capacity budgeted for one instanced-skinned draw's bone-palette buffer.
    /// OpenGL 3.1+ (the baseline this feature needs for texture buffer objects) guarantees
    /// <c>GL_MAX_TEXTURE_BUFFER_SIZE</c> of at least 65536 texels on every conformant implementation,
    /// so a chunk sized to this constant never exceeds the real buffer texture limit regardless of
    /// GPU — see docs/instancing.md. At <see cref="BonePaletteBuffer.TexelsPerBone"/> (4) texels per
    /// bone, that's 16384 bones per draw — e.g. 256 fully-rigged 64-bone characters, or thousands of
    /// simpler ones.
    /// </summary>
    public const int MaxPaletteTexels = 65536;

    /// <summary>
    /// Groups instance indices <c>[0, boneCounts.Count)</c> into contiguous <c>(Start, Count)</c>
    /// chunks whose total texel usage (each instance's bone count times
    /// <see cref="BonePaletteBuffer.TexelsPerBone"/>) never exceeds <see cref="MaxPaletteTexels"/>. A
    /// single instance whose own palette alone exceeds the cap still gets its own one-instance chunk
    /// rather than being dropped. Returns an empty list for an empty input.
    /// </summary>
    public static List<(int Start, int Count)> PlanChunks(IReadOnlyList<int> boneCounts)
    {
        var chunks = new List<(int Start, int Count)>();
        if (boneCounts.Count == 0)
            return chunks;

        var chunkStart = 0;
        var texelsInChunk = 0;
        for (var i = 0; i < boneCounts.Count; i++)
        {
            var texels = boneCounts[i] * BonePaletteBuffer.TexelsPerBone;
            if (i > chunkStart && texelsInChunk + texels > MaxPaletteTexels)
            {
                chunks.Add((chunkStart, i - chunkStart));
                chunkStart = i;
                texelsInChunk = 0;
            }
            texelsInChunk += texels;
        }
        chunks.Add((chunkStart, boneCounts.Count - chunkStart));

        return chunks;
    }
}

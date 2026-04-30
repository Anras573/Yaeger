namespace Yaeger.Rendering;

internal static class QuadIndexing
{
    public const int VerticesPerQuad = 4;
    public const int IndicesPerQuad = 6;

    /// <summary>
    /// Builds the static element-array contents for a batched quad renderer.
    /// Each quad is two triangles sharing vertices 1 and 3, in the winding order
    /// expected by the vertex layout used across <see cref="Renderer"/> and
    /// <see cref="TextRenderer"/>: (0,1,3) + (1,2,3).
    /// </summary>
    public static uint[] GenerateQuadIndices(int maxQuads)
    {
        var buffer = new uint[maxQuads * IndicesPerQuad];
        for (uint q = 0; q < maxQuads; q++)
        {
            var vertexOffset = q * VerticesPerQuad;
            var indexOffset = q * IndicesPerQuad;

            buffer[indexOffset + 0] = vertexOffset + 0;
            buffer[indexOffset + 1] = vertexOffset + 1;
            buffer[indexOffset + 2] = vertexOffset + 3;
            buffer[indexOffset + 3] = vertexOffset + 1;
            buffer[indexOffset + 4] = vertexOffset + 2;
            buffer[indexOffset + 5] = vertexOffset + 3;
        }
        return buffer;
    }
}

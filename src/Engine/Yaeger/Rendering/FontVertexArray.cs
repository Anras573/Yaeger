using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class FontVertexArray : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    private const uint VertexSize = 9; // 3 for position, 2 for texture coordinates, 4 for color
    private const int PositionCount = 3; // x, y, z
    private const int TexCoordCount = 2; // u, v
    private const int ColorCount = 4; // r, g, b, a

    public FontVertexArray(GL gl, Buffer<float> vbo, Buffer<uint> ebo)
    {
        _gl = gl;
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();

        VertexAttributeFloatPointer(0, PositionCount, VertexSize, 0);
        VertexAttributeFloatPointer(1, TexCoordCount, VertexSize, PositionCount);
        VertexAttributeFloatPointer(2, ColorCount, VertexSize, PositionCount + TexCoordCount);
    }

    private unsafe void VertexAttributeFloatPointer(uint index, int count, uint vertexSize, int offset)
    {
        _gl.VertexAttribPointer(index, count, VertexAttribPointerType.Float, false, vertexSize * sizeof(float), (void*)(offset * sizeof(float)));
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind() => _gl.BindVertexArray(_handle);
    public void Unbind() => _gl.BindVertexArray(0);

    public void Dispose() => _gl.DeleteVertexArray(_handle);
}
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Vertex array for debug line rendering. Position-only layout (3 floats per vertex).
/// </summary>
public class DebugVertexArray : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    private const uint VertexSize = 3; // x, y, z
    private const int PositionCount = 3;

    public DebugVertexArray(GL gl, Buffer<float> vbo)
    {
        _gl = gl;
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();

        VertexAttributeFloatPointer(0, PositionCount, VertexSize, 0);
    }

    private unsafe void VertexAttributeFloatPointer(
        uint index,
        int count,
        uint vertexSize,
        int offset
    )
    {
        _gl.VertexAttribPointer(
            index,
            count,
            VertexAttribPointerType.Float,
            false,
            vertexSize * sizeof(float),
            (void*)(offset * sizeof(float))
        );
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind() => _gl.BindVertexArray(_handle);

    public void Unbind() => _gl.BindVertexArray(0);

    public void Dispose() => _gl.DeleteVertexArray(_handle);
}

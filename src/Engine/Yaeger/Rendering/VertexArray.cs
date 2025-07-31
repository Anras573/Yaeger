using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class VertexArray : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    
    private const uint VertexSize = 5; // 3 for position, 2 for texture coordinates
    private const int PositionCount = 3; // x, y, z
    private const int TexCoordCount = 2; // u, v

    public VertexArray(GL gl, Buffer<float> vbo, Buffer<uint> ebo)
    {
        _gl = gl;
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();
        
        VertexAttributeFloatPointer(0, PositionCount, VertexSize, 0);
        VertexAttributeFloatPointer(1, TexCoordCount, VertexSize, 3);
    }
    
    private unsafe void VertexAttributeFloatPointer(uint index, int count, uint vertexSize, int offset)
    {
        _gl.VertexAttribPointer(index, count, VertexAttribPointerType.Float, false, vertexSize * sizeof(float), (void*) (offset * sizeof(float)));
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind() => _gl.BindVertexArray(_handle);
    public void Unbind() => _gl.BindVertexArray(0);

    public void Dispose() => _gl.DeleteVertexArray(_handle);
}


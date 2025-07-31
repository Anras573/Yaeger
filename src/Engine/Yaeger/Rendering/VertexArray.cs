using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class VertexArray<TVertex, TIndex> : IDisposable
    where TVertex : unmanaged
    where TIndex : unmanaged
{
    private readonly GL _gl;
    private readonly uint _handle;

    public VertexArray(GL gl, Buffer<TVertex> vbo, Buffer<TIndex> ebo)
    {
        _gl = gl;
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();
        
        VertexAttributeFloatPointer(0, 3, 5, 0);
        VertexAttributeFloatPointer(1, 2, 5, 3);
    }
    
    private unsafe void VertexAttributeFloatPointer(uint index, int count, uint vertexSize, int offset)
    {
        _gl.VertexAttribPointer(index, count, VertexAttribPointerType.Float, false, vertexSize * (uint) sizeof(TVertex), (void*) (offset * sizeof(TVertex)));
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind() => _gl.BindVertexArray(_handle);
    public void Unbind() => _gl.BindVertexArray(0);

    public void Dispose()
    {
        _gl.DeleteVertexArray(_handle);
    }
}


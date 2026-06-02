using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public sealed class GpuMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly Buffer<Vertex3D> _vbo;
    private readonly Buffer<uint> _ebo;
    private readonly uint _indexCount;

    // Vertex layout: Position (3f) | Normal (3f) | TexCoord (2f)
    private const uint VertexSize = 8;
    private const int PositionCount = 3;
    private const int NormalCount = 3;
    private const int TexCoordCount = 2;
    private const int NormalOffset = PositionCount;
    private const int TexCoordOffset = PositionCount + NormalCount;

    public unsafe GpuMesh(GL gl, MeshData data)
    {
        _gl = gl;
        _indexCount = (uint)data.Indices.Length;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = new Buffer<Vertex3D>(gl, data.Vertices, BufferTargetARB.ArrayBuffer);
        _ebo = new Buffer<uint>(gl, data.Indices, BufferTargetARB.ElementArrayBuffer);

        VertexAttributeFloatPointer(0, PositionCount, VertexSize, 0);
        VertexAttributeFloatPointer(1, NormalCount, VertexSize, NormalOffset);
        VertexAttributeFloatPointer(2, TexCoordCount, VertexSize, TexCoordOffset);

        _gl.BindVertexArray(0);
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

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(
            PrimitiveType.Triangles,
            _indexCount,
            DrawElementsType.UnsignedInt,
            (void*)0
        );
    }

    public void Dispose()
    {
        _vbo.Dispose();
        _ebo.Dispose();
        _gl.DeleteVertexArray(_vao);
    }
}

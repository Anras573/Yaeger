using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public sealed class GpuMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly Buffer<Vertex3D> _vbo;
    private readonly Buffer<uint> _ebo;
    private readonly uint _indexCount;

    public unsafe GpuMesh(GL gl, MeshData data)
    {
        _gl = gl;
        _indexCount = (uint)data.Indices.Length;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = new Buffer<Vertex3D>(gl, data.Vertices, BufferTargetARB.ArrayBuffer);
        _vbo.Bind();
        _ebo = new Buffer<uint>(gl, data.Indices, BufferTargetARB.ElementArrayBuffer);
        _ebo.Bind();

        var stride = (uint)sizeof(Vertex3D);
        SetupAttrib(0, 3, stride, OffsetOf(nameof(Vertex3D.Position)));
        SetupAttrib(1, 3, stride, OffsetOf(nameof(Vertex3D.Normal)));
        SetupAttrib(2, 2, stride, OffsetOf(nameof(Vertex3D.TexCoord)));
        SetupAttrib(3, 3, stride, OffsetOf(nameof(Vertex3D.Tangent)));

        _gl.BindVertexArray(0);
    }

    private static uint OffsetOf(string fieldName) =>
        (uint)(nint)Marshal.OffsetOf<Vertex3D>(fieldName);

    private unsafe void SetupAttrib(uint index, int count, uint stride, uint offset)
    {
        _gl.VertexAttribPointer(
            index,
            count,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)offset
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
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _vbo.Dispose();
        _ebo.Dispose();
    }
}

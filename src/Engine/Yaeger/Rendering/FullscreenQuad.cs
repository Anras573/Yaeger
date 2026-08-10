using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// The NDC-space quad (two triangles) every full-screen post-processing pass draws — shared by
/// <see cref="PostProcessStack"/>'s own passthrough blit and every <see cref="IPostProcessEffect"/>,
/// so the vertex data is uploaded once regardless of how many effects are chained.
/// </summary>
public sealed class FullscreenQuad : IDisposable
{
    // pos.xy, uv.xy — same layout/winding as IblPrefilter's internal quad mesh.
    private static readonly float[] Vertices =
    [
        -1f,
        1f,
        0f,
        1f,
        -1f,
        -1f,
        0f,
        0f,
        1f,
        -1f,
        1f,
        0f,
        -1f,
        1f,
        0f,
        1f,
        1f,
        -1f,
        1f,
        0f,
        1f,
        1f,
        1f,
        1f,
    ];

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;

    public unsafe FullscreenQuad(GL gl)
    {
        _gl = gl;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = Vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(Vertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw
            );
        }

        _gl.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            4 * sizeof(float),
            (void*)0
        );
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            4 * sizeof(float),
            (void*)(2 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
    }
}

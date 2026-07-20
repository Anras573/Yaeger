using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Rendering;
using Shader = Yaeger.Rendering.Shader;

namespace Yaeger.Inspector;

/// <summary>
/// Draws the editor gizmo overlay: a batch of world-space coloured lines transformed by the active
/// camera's view-projection. Unlike <c>PhysicsDebugRenderer</c> (which draws in NDC), this renderer
/// applies <c>uViewProj</c> so gizmos line up with the 3D scene. Depth testing is left to the caller
/// (the inspector draws after the 3D pass has disabled it) so gizmos stay visible through geometry —
/// handy for finding a light tucked behind a wall.
/// </summary>
public sealed class GizmoRenderer : IDisposable
{
    private const int MaxLines = 8192;
    private const int FloatsPerVertex = 7; // xyz + rgba
    private const int VerticesPerLine = 2;

    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load("Gizmo.vert");
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load("Gizmo.frag");

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly Buffer<float> _vbo;
    private readonly uint _vao;
    private readonly float[] _vertices;

    public GizmoRenderer(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _vertices = new float[MaxLines * VerticesPerLine * FloatsPerVertex];

        _vbo = new Buffer<float>(
            gl,
            _vertices,
            BufferTargetARB.ArrayBuffer,
            BufferUsageARB.DynamicDraw
        );

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);
        _vbo.Bind();
        ConfigureAttributes();
        _gl.BindVertexArray(0);
    }

    private unsafe void ConfigureAttributes()
    {
        var stride = (uint)(FloatsPerVertex * sizeof(float));
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            1,
            4,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(3 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(1);
    }

    /// <summary>
    /// Draws the supplied lines in world space using <paramref name="viewProj"/>. Lines beyond the
    /// internal capacity are dropped. A no-op when the list is empty.
    /// </summary>
    /// <param name="depthTest">
    /// When <c>false</c> (the default) gizmos draw on top of all geometry; when <c>true</c> they are
    /// depth-tested against the scene so geometry in front occludes them.
    /// </param>
    /// <param name="lineWidth">Width of the drawn lines in pixels (driver-dependent clamping applies).</param>
    public unsafe void Render(
        IReadOnlyList<GizmoLine> lines,
        Matrix4x4 viewProj,
        bool depthTest = false,
        float lineWidth = 1f
    )
    {
        if (lines.Count == 0)
            return;

        var lineCount = Math.Min(lines.Count, MaxLines);
        var floatCount = lineCount * VerticesPerLine * FloatsPerVertex;

        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i];
            var offset = i * VerticesPerLine * FloatsPerVertex;
            WriteVertex(offset, line.Start, line.Color);
            WriteVertex(offset + FloatsPerVertex, line.End, line.Color);
        }

        // Alpha-blend so coloured gizmos read well over the scene.
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Depth testing is off by default (the 3D pass already left it off) so lines draw on top of
        // occluding geometry; enable it when the caller wants gizmos hidden behind the scene.
        if (depthTest)
            _gl.Enable(EnableCap.DepthTest);
        else
            _gl.Disable(EnableCap.DepthTest);

        // Fall back to 1px for any non-positive or non-finite width: a NaN already fails the > 0
        // test, but +Infinity would otherwise pass straight through to glLineWidth and produce a GL
        // error / undefined rendering.
        _gl.LineWidth(float.IsFinite(lineWidth) && lineWidth > 0f ? lineWidth : 1f);

        _shader.Bind();
        _shader.SetUniformMatrix4("uViewProj", viewProj);

        _gl.BindVertexArray(_vao);
        _vbo.Bind();

        var byteCount = (nuint)(floatCount * sizeof(float));
        fixed (float* ptr = _vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, byteCount, ptr);
        }

        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(lineCount * VerticesPerLine));

        _gl.BindVertexArray(0);
        _shader.Unbind();

        // Restore the baseline state the inspector path relies on: the 3D pass leaves depth testing
        // off before gizmos draw, and the ImGui overlay rendered next expects the default line
        // width. Undo whatever we changed so neither the overlay nor later passes inherit it.
        if (depthTest)
            _gl.Disable(EnableCap.DepthTest);
        _gl.LineWidth(1f);
    }

    private void WriteVertex(int offset, Vector3 position, Vector4 color)
    {
        _vertices[offset + 0] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = position.Z;
        _vertices[offset + 3] = color.X;
        _vertices[offset + 4] = color.Y;
        _vertices[offset + 5] = color.Z;
        _vertices[offset + 6] = color.W;
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _vbo.Dispose();
        _shader.Dispose();
    }
}

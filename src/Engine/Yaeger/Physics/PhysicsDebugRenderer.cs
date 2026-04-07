using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Rendering;
using Yaeger.Windowing;
using GL = Silk.NET.OpenGL.GL;

namespace Yaeger.Physics;

/// <summary>
/// Renders configurable wireframe outlines for physics colliders using GL_LINES.
/// </summary>
public class PhysicsDebugRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly World _world;
    private readonly Shader _shader;
    private readonly DebugVertexArray _vao;
    private readonly Buffer<float> _vbo;

    private const int MaxLineSegments = 4096;
    private const int FloatsPerVertex = 3;
    private const int VerticesPerLine = 2;
    private const int CircleSegments = 32;

    private readonly float[] _vertexBuffer;
    private int _vertexCount;

    /// <summary>
    /// The color used for collider wireframes. Default is green.
    /// </summary>
    public Vector4 ColliderColor { get; set; } = new(0.0f, 1.0f, 0.0f, 1.0f);

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;

        void main()
        {
            gl_Position = vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        uniform vec4 uColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = uColor;
        }
        """;

    public PhysicsDebugRenderer(Window window, World world)
    {
        _gl = window.Gl;
        _world = world;

        _shader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vertexBuffer = new float[MaxLineSegments * VerticesPerLine * FloatsPerVertex];

        _vbo = new Buffer<float>(
            _gl,
            _vertexBuffer,
            Silk.NET.OpenGL.BufferTargetARB.ArrayBuffer,
            Silk.NET.OpenGL.BufferUsageARB.DynamicDraw
        );

        _vao = new DebugVertexArray(_gl, _vbo);
        _vao.Unbind();
    }

    /// <summary>
    /// Renders wireframe outlines for all entities that have collider components.
    /// Call this after your main render pass so the debug lines draw on top.
    /// </summary>
    public void Render()
    {
        _vertexCount = 0;

        // Collect box collider outlines
        foreach (
            (Entity _, BoxCollider2D collider, Transform2D transform) in _world.Query<
                BoxCollider2D,
                Transform2D
            >()
        )
        {
            var center = transform.Position + collider.Offset;
            AddBox(center, collider.HalfSize);
        }

        // Collect circle collider outlines
        foreach (
            (Entity _, CircleCollider2D collider, Transform2D transform) in _world.Query<
                CircleCollider2D,
                Transform2D
            >()
        )
        {
            var center = transform.Position + collider.Offset;
            AddCircle(center, collider.Radius);
        }

        if (_vertexCount == 0)
            return;

        Flush();
    }

    private void AddBox(Vector2 center, Vector2 halfSize)
    {
        var topLeft = new Vector2(center.X - halfSize.X, center.Y + halfSize.Y);
        var topRight = new Vector2(center.X + halfSize.X, center.Y + halfSize.Y);
        var bottomRight = new Vector2(center.X + halfSize.X, center.Y - halfSize.Y);
        var bottomLeft = new Vector2(center.X - halfSize.X, center.Y - halfSize.Y);

        AddLine(topLeft, topRight);
        AddLine(topRight, bottomRight);
        AddLine(bottomRight, bottomLeft);
        AddLine(bottomLeft, topLeft);
    }

    private void AddCircle(Vector2 center, float radius)
    {
        var angleStep = MathF.PI * 2.0f / CircleSegments;

        for (var i = 0; i < CircleSegments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var p1 = new Vector2(
                center.X + MathF.Cos(angle1) * radius,
                center.Y + MathF.Sin(angle1) * radius
            );
            var p2 = new Vector2(
                center.X + MathF.Cos(angle2) * radius,
                center.Y + MathF.Sin(angle2) * radius
            );

            AddLine(p1, p2);
        }
    }

    private void AddLine(Vector2 from, Vector2 to)
    {
        if (_vertexCount + 2 > MaxLineSegments * VerticesPerLine)
        {
            Flush();
            _vertexCount = 0;
        }

        var offset = _vertexCount * FloatsPerVertex;

        _vertexBuffer[offset + 0] = from.X;
        _vertexBuffer[offset + 1] = from.Y;
        _vertexBuffer[offset + 2] = 0.0f;

        _vertexBuffer[offset + 3] = to.X;
        _vertexBuffer[offset + 4] = to.Y;
        _vertexBuffer[offset + 5] = 0.0f;

        _vertexCount += 2;
    }

    private unsafe void Flush()
    {
        _shader.Bind();
        _shader.SetUniformVec4("uColor", ColliderColor);

        _vao.Bind();
        _vbo.Bind();

        var byteCount = (nuint)(_vertexCount * FloatsPerVertex * sizeof(float));
        fixed (float* ptr = _vertexBuffer)
        {
            _gl.BufferSubData(Silk.NET.OpenGL.BufferTargetARB.ArrayBuffer, 0, byteCount, ptr);
        }

        _gl.DrawArrays(Silk.NET.OpenGL.PrimitiveType.Lines, 0, (uint)_vertexCount);

        _vao.Unbind();
        _shader.Unbind();
    }

    public void Dispose()
    {
        _vao.Dispose();
        _vbo.Dispose();
        _shader.Dispose();
        GC.SuppressFinalize(this);
    }
}

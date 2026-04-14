using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

public class Renderer
{
    private readonly GL _gl;
    private readonly VertexArray _vao;
    private readonly Buffer<float> _vbo;

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform mat4 uTransform;

        out vec2 vTexCoord;

        void main()
        {
            gl_Position = uTransform * vec4(aPosition, 1.0);
            vTexCoord = aTexCoord;
        }
        """;
    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vTexCoord;
        out vec4 FragColor;

        uniform sampler2D uTexture;

        void main()
        {
            FragColor = texture(uTexture, vTexCoord);
        }
        """;

    private readonly TextureManager _textureManager;
    private readonly Shader _textureShader;

    // Mutable vertex buffer: 4 vertices × 5 floats (x, y, z, u, v)
    private readonly float[] _vertices = new float[20];

    private static readonly uint[] Indices = [0, 1, 3, 1, 2, 3];

    public Renderer(Window window)
    {
        _gl = window.Gl;

        _textureManager = new TextureManager(_gl);
        _textureShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vbo = new Buffer<float>(
            _gl,
            _vertices,
            BufferTargetARB.ArrayBuffer,
            BufferUsageARB.DynamicDraw
        );
        var ebo = new Buffer<uint>(_gl, Indices, BufferTargetARB.ElementArrayBuffer);
        _vao = new VertexArray(_gl, _vbo, ebo);

        CheckGlError();

        Console.WriteLine(
            $"GL initialized: {_gl.GetStringS(GLEnum.Version)}, {_gl.GetStringS(GLEnum.Renderer)}"
        );
    }

    private void CheckGlError([CallerMemberName] string context = "")
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Console.WriteLine($"OpenGL error after {context}: {error}");
        }
    }

    public void BeginFrame()
    {
        // Query the current framebuffer size
        var viewport = new int[4];
        _gl.GetInteger(GLEnum.Viewport, viewport);
        var width = viewport[2];
        var height = viewport[3];

        // Always set the viewport to the current framebuffer size
        _gl.Viewport(0, 0, (uint)width, (uint)height);

        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        CheckGlError();
    }

    public void EndFrame() { /* No-op for now */
    }

    /// <summary>Draws a quad using the full texture (UV 0,0 → 1,1).</summary>
    public void DrawQuad(Matrix4x4 model, string texturePath) =>
        DrawQuad(model, texturePath, Vector2.Zero, Vector2.One);

    /// <summary>Draws a quad using a sub-region of the texture defined by UV coordinates.</summary>
    public unsafe void DrawQuad(Matrix4x4 model, string texturePath, Vector2 uvMin, Vector2 uvMax)
    {
        // Base positions of a unit quad (centred at origin)
        ReadOnlySpan<Vector3> positions =
        [
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        ];

        // UV corners matching the position layout
        ReadOnlySpan<Vector2> uvs =
        [
            new Vector2(uvMax.X, uvMax.Y),
            new Vector2(uvMax.X, uvMin.Y),
            new Vector2(uvMin.X, uvMin.Y),
            new Vector2(uvMin.X, uvMax.Y),
        ];

        for (int i = 0; i < 4; i++)
        {
            int offset = i * 5;
            _vertices[offset + 0] = positions[i].X;
            _vertices[offset + 1] = positions[i].Y;
            _vertices[offset + 2] = positions[i].Z;
            _vertices[offset + 3] = uvs[i].X;
            _vertices[offset + 4] = uvs[i].Y;
        }

        _vbo.Bind();
        fixed (float* v = _vertices)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(_vertices.Length * sizeof(float)),
                v
            );
        }

        var texture = _textureManager.Get(texturePath);
        _textureShader.Bind();
        _textureShader.SetUniformMatrix4("uTransform", model);

        texture.Bind();
        _vao.Bind();

        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)Indices.Length,
            DrawElementsType.UnsignedInt,
            null
        );

        _vao.Unbind();
        texture.Unbind();
        _textureShader.Unbind();
    }
}

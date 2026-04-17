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

    private static readonly float[] FullQuadVertices =
    [
        // Vertex 0: top-right
        0.5f,
        0.5f,
        0f,
        1f,
        1f,
        // Vertex 1: bottom-right
        0.5f,
        -0.5f,
        0f,
        1f,
        0f,
        // Vertex 2: bottom-left
        -0.5f,
        -0.5f,
        0f,
        0f,
        0f,
        // Vertex 3: top-left
        -0.5f,
        0.5f,
        0f,
        0f,
        1f,
    ];

    // Mutable vertex buffer: 4 vertices × 5 floats (x, y, z, u, v)
    private readonly float[] _vertices = new float[20];
    private bool _fullUvBufferLoaded = true;
    private bool _hasLastCustomUv;
    private Vector2 _lastCustomUvMin;
    private Vector2 _lastCustomUvMax;

    private static readonly uint[] Indices = [0, 1, 3, 1, 2, 3];

    public Renderer(Window window)
    {
        _gl = window.Gl;

        _textureManager = new TextureManager(_gl);
        _textureShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vbo = new Buffer<float>(
            _gl,
            FullQuadVertices,
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
    public void DrawQuad(Matrix4x4 model, string texturePath)
    {
        if (!_fullUvBufferLoaded)
        {
            UploadVertices(FullQuadVertices);
            _fullUvBufferLoaded = true;
        }

        DrawQuadCore(model, texturePath);
    }

    /// <summary>Draws a quad using a sub-region of the texture defined by UV coordinates.</summary>
    public unsafe void DrawQuad(Matrix4x4 model, string texturePath, Vector2 uvMin, Vector2 uvMax)
    {
        if (uvMin == Vector2.Zero && uvMax == Vector2.One)
        {
            DrawQuad(model, texturePath);
            return;
        }

        if (
            !_fullUvBufferLoaded
            && _hasLastCustomUv
            && uvMin == _lastCustomUvMin
            && uvMax == _lastCustomUvMax
        )
        {
            DrawQuadCore(model, texturePath);
            return;
        }

        // Vertex 0: top-right
        _vertices[0] = 0.5f;
        _vertices[1] = 0.5f;
        _vertices[2] = 0f;
        _vertices[3] = uvMax.X;
        _vertices[4] = uvMax.Y;

        // Vertex 1: bottom-right
        _vertices[5] = 0.5f;
        _vertices[6] = -0.5f;
        _vertices[7] = 0f;
        _vertices[8] = uvMax.X;
        _vertices[9] = uvMin.Y;

        // Vertex 2: bottom-left
        _vertices[10] = -0.5f;
        _vertices[11] = -0.5f;
        _vertices[12] = 0f;
        _vertices[13] = uvMin.X;
        _vertices[14] = uvMin.Y;

        // Vertex 3: top-left
        _vertices[15] = -0.5f;
        _vertices[16] = 0.5f;
        _vertices[17] = 0f;
        _vertices[18] = uvMin.X;
        _vertices[19] = uvMax.Y;

        UploadVertices(_vertices);
        _fullUvBufferLoaded = false;
        _hasLastCustomUv = true;
        _lastCustomUvMin = uvMin;
        _lastCustomUvMax = uvMax;
        DrawQuadCore(model, texturePath);
    }

    private unsafe void UploadVertices(float[] vertices)
    {
        _vbo.Bind();
        fixed (float* vertexPtr = vertices)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(vertices.Length * sizeof(float)),
                vertexPtr
            );
        }
    }

    private unsafe void DrawQuadCore(Matrix4x4 model, string texturePath)
    {
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

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// Renders quads in batches grouped by texture to minimise OpenGL state changes.
/// Submit quads with <see cref="SubmitQuad(Matrix4x4, string)"/> or its UV-aware
/// overload; draw calls are issued during <see cref="EndFrame"/>.
/// </summary>
public class Renderer : IDisposable
{
    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = QuadIndexing.VerticesPerQuad;
    private const int IndicesPerQuad = QuadIndexing.IndicesPerQuad;
    private const int FloatsPerVertex = 5; // 3 position + 2 texcoord

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform mat4 uViewProj;

        out vec2 vTexCoord;

        void main()
        {
            gl_Position = uViewProj * vec4(aPosition, 1.0);
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

    private readonly GL _gl;
    private readonly TextureManager _textureManager;
    private readonly Shader _textureShader;
    private readonly VertexArray _vao;
    private readonly Buffer<float> _vbo;
    private readonly Buffer<uint> _ebo;

    private readonly float[] _vertexBuffer = new float[
        MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex
    ];

    private readonly Dictionary<string, List<QuadSubmission>> _batchQueue = new();

    private Matrix4x4 _viewProjection = Matrix4x4.Identity;

    public Renderer(Window window)
    {
        _gl = window.Gl;
        _textureManager = new TextureManager(_gl);
        _textureShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vbo = new Buffer<float>(
            _gl,
            _vertexBuffer,
            BufferTargetARB.ArrayBuffer,
            BufferUsageARB.DynamicDraw
        );
        _ebo = new Buffer<uint>(
            _gl,
            QuadIndexing.GenerateQuadIndices(MaxQuadsPerBatch),
            BufferTargetARB.ElementArrayBuffer
        );
        _vao = new VertexArray(_gl, _vbo, _ebo);

        CheckGlError();

        Console.WriteLine(
            $"GL initialized: {_gl.GetStringS(GLEnum.Version)}, {_gl.GetStringS(GLEnum.Renderer)}"
        );
    }

    public void BeginFrame()
    {
        // Window owns the viewport — it syncs on resize, so the renderer doesn't touch it here.
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        foreach (var submissions in _batchQueue.Values)
        {
            submissions.Clear();
        }

        CheckGlError();
    }

    /// <summary>
    /// Sets the view-projection matrix applied to every quad. Call once per frame before
    /// <see cref="EndFrame"/>. Resets to identity across construction; if never called, quads
    /// are rendered in NDC directly (the pre-camera default).
    /// </summary>
    public void SetCamera(Matrix4x4 viewProjection)
    {
        _viewProjection = viewProjection;
    }

    /// <summary>Queues a quad drawn with the full texture (UV 0,0 → 1,1).</summary>
    public void SubmitQuad(Matrix4x4 model, string texturePath)
    {
        SubmitQuad(model, texturePath, Vector2.Zero, Vector2.One);
    }

    /// <summary>Queues a quad drawn with a sub-region of the texture defined by UV coordinates.</summary>
    public void SubmitQuad(Matrix4x4 model, string texturePath, Vector2 uvMin, Vector2 uvMax)
    {
        ref var submissions = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _batchQueue,
            texturePath,
            out _
        );
        submissions ??= [];
        submissions.Add(new QuadSubmission(model, uvMin, uvMax));
    }

    /// <summary>Flushes all queued quads. One draw call per texture (split into batches of 1000).</summary>
    public void EndFrame()
    {
        foreach (var (texturePath, submissions) in _batchQueue)
        {
            if (submissions.Count == 0)
            {
                continue;
            }
            RenderBatch(texturePath, submissions);
        }
    }

    private void RenderBatch(string texturePath, List<QuadSubmission> submissions)
    {
        var texture = _textureManager.Get(texturePath);
        _textureShader.Bind();
        _textureShader.SetUniformMatrix4("uViewProj", _viewProjection);
        texture.Bind();
        _vao.Bind();
        _vbo.Bind();

        for (var i = 0; i < submissions.Count; i += MaxQuadsPerBatch)
        {
            var batchSize = Math.Min(MaxQuadsPerBatch, submissions.Count - i);
            FillVertexBuffer(submissions, i, batchSize);
            DrawBatch(batchSize);
        }

        _vao.Unbind();
        texture.Unbind();
        _textureShader.Unbind();
    }

    private void FillVertexBuffer(List<QuadSubmission> submissions, int startIndex, int count)
    {
        // Corner order must match the quad winding baked into QuadIndexing: TR, BR, BL, TL.
        ReadOnlySpan<Vector3> basePositions =
        [
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        ];

        for (var q = 0; q < count; q++)
        {
            var submission = submissions[startIndex + q];
            var transform = submission.Transform;
            var uvMin = submission.UvMin;
            var uvMax = submission.UvMax;

            ReadOnlySpan<Vector2> uvs =
            [
                new Vector2(uvMax.X, uvMax.Y),
                new Vector2(uvMax.X, uvMin.Y),
                new Vector2(uvMin.X, uvMin.Y),
                new Vector2(uvMin.X, uvMax.Y),
            ];

            var vertexOffset = q * VerticesPerQuad * FloatsPerVertex;
            for (var c = 0; c < VerticesPerQuad; c++)
            {
                WriteVertex(
                    vertexOffset + c * FloatsPerVertex,
                    basePositions[c],
                    in transform,
                    uvs[c]
                );
            }
        }
    }

    private void WriteVertex(int offset, Vector3 basePosition, in Matrix4x4 transform, Vector2 uv)
    {
        var transformed = Vector3.Transform(basePosition, transform);
        _vertexBuffer[offset + 0] = transformed.X;
        _vertexBuffer[offset + 1] = transformed.Y;
        _vertexBuffer[offset + 2] = transformed.Z;
        _vertexBuffer[offset + 3] = uv.X;
        _vertexBuffer[offset + 4] = uv.Y;
    }

    private unsafe void DrawBatch(int quadCount)
    {
        var vertexCount = quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* vertices = _vertexBuffer)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(vertexCount * sizeof(float)),
                vertices
            );
        }

        var indexCount = quadCount * IndicesPerQuad;
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)indexCount,
            DrawElementsType.UnsignedInt,
            null
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

    public void Dispose()
    {
        _vao.Dispose();
        _vbo.Dispose();
        _ebo.Dispose();
        _textureShader.Dispose();
        _textureManager.Dispose();
    }

    private readonly record struct QuadSubmission(
        Matrix4x4 Transform,
        Vector2 UvMin,
        Vector2 UvMax
    );
}

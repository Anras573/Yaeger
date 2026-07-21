using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Yaeger.Platform;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// Renders quads in deterministic submission order, batching contiguous quads that
/// share a texture to minimise OpenGL state changes.
/// Submit quads with <see cref="SubmitQuad(Matrix4x4, string)"/> or its UV-aware overload;
/// draw calls are issued when queued quads are flushed via
/// <see cref="FlushQueuedQuads"/> or <see cref="EndFrame"/>.
/// </summary>
public class Renderer : IRenderSurface, IDisposable
{
    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = QuadIndexing.VerticesPerQuad;
    private const int IndicesPerQuad = QuadIndexing.IndicesPerQuad;
    private const int FloatsPerVertex = 9; // 3 position + 2 texcoord + 4 color

    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load("Renderer.vert");
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "Renderer.frag"
    );

    private readonly GL _gl;
    private readonly TextureManager _textureManager;
    private readonly Shader _textureShader;
    private readonly VertexArray _vao;
    private readonly Buffer<float> _vbo;
    private readonly Buffer<uint> _ebo;

    private readonly float[] _vertexBuffer = new float[
        MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex
    ];

    private readonly List<QuadSubmission> _submissionQueue = [];

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

        _submissionQueue.Clear();

        CheckGlError();
    }

    /// <summary>
    /// Sets the view-projection matrix applied to every quad. Call once per frame before
    /// <see cref="EndFrame"/>. Defaults to <see cref="Matrix4x4.Identity"/>, which renders
    /// quads in NDC directly.
    /// </summary>
    public void SetCamera(Matrix4x4 viewProjection)
    {
        _viewProjection = viewProjection;
    }

    /// <summary>Queues a quad drawn with the full texture and default white tint.</summary>
    public void SubmitQuad(Matrix4x4 model, string texturePath)
    {
        SubmitQuad(model, texturePath, Vector4.One);
    }

    /// <summary>Queues a quad drawn with the full texture (UV 0,0 → 1,1), tinted by <paramref name="color"/>.</summary>
    public void SubmitQuad(Matrix4x4 model, string texturePath, Vector4 color)
    {
        SubmitQuad(model, texturePath, Vector2.Zero, Vector2.One, color);
    }

    /// <summary>
    /// Queues a quad drawn with a sub-region of the texture and default white tint.
    /// </summary>
    public void SubmitQuad(Matrix4x4 model, string texturePath, Vector2 uvMin, Vector2 uvMax)
    {
        SubmitQuad(model, texturePath, uvMin, uvMax, Vector4.One);
    }

    /// <summary>Queues a quad drawn with a sub-region of the texture defined by UV coordinates.</summary>
    public void SubmitQuad(
        Matrix4x4 model,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    )
    {
        _submissionQueue.Add(new QuadSubmission(texturePath, model, uvMin, uvMax, color));
    }

    /// <summary>
    /// Flushes all queued quads, preserving submission order and batching contiguous
    /// quads that share a texture (up to 1000 quads per draw call), by delegating to
    /// <see cref="FlushQueuedQuads"/>.
    /// </summary>
    public void EndFrame()
    {
        FlushQueuedQuads();
    }

    /// <summary>
    /// Flushes queued quads immediately while preserving submission order.
    /// </summary>
    public void FlushQueuedQuads()
    {
        if (_submissionQueue.Count == 0)
            return;

        var startIndex = 0;
        while (startIndex < _submissionQueue.Count)
        {
            var texturePath = _submissionQueue[startIndex].TexturePath;
            var batchSize = 1;
            while (
                startIndex + batchSize < _submissionQueue.Count
                && batchSize < MaxQuadsPerBatch
                && _submissionQueue[startIndex + batchSize].TexturePath == texturePath
            )
            {
                batchSize++;
            }

            RenderBatch(texturePath, startIndex, batchSize);
            startIndex += batchSize;
        }

        _submissionQueue.Clear();
    }

    private void RenderBatch(string texturePath, int startIndex, int batchSize)
    {
        var texture = _textureManager.Get(texturePath);
        _textureShader.Bind();
        _textureShader.SetUniformMatrix4("uViewProj", _viewProjection);
        texture.Bind();
        _vao.Bind();
        _vbo.Bind();

        FillVertexBuffer(startIndex, batchSize);
        DrawBatch(batchSize);

        _vao.Unbind();
        texture.Unbind();
        _textureShader.Unbind();
    }

    private void FillVertexBuffer(int startIndex, int count)
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
            var submission = _submissionQueue[startIndex + q];
            var transform = submission.Transform;
            var uvMin = submission.UvMin;
            var uvMax = submission.UvMax;
            var color = submission.Color;

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
                    uvs[c],
                    color
                );
            }
        }
    }

    private void WriteVertex(
        int offset,
        Vector3 basePosition,
        in Matrix4x4 transform,
        Vector2 uv,
        Vector4 color
    )
    {
        var transformed = Vector3.Transform(basePosition, transform);
        _vertexBuffer[offset + 0] = transformed.X;
        _vertexBuffer[offset + 1] = transformed.Y;
        _vertexBuffer[offset + 2] = transformed.Z;
        _vertexBuffer[offset + 3] = uv.X;
        _vertexBuffer[offset + 4] = uv.Y;
        _vertexBuffer[offset + 5] = color.X;
        _vertexBuffer[offset + 6] = color.Y;
        _vertexBuffer[offset + 7] = color.Z;
        _vertexBuffer[offset + 8] = color.W;
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
        string TexturePath,
        Matrix4x4 Transform,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color
    );
}

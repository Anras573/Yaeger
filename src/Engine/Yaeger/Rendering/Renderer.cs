using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    private readonly TexturePathTable _texturePaths = new();
    private readonly List<Texture?> _textures = [];

    private readonly CameraMatrixTracker _camera = new();

    /// <summary>
    /// The texture cache this renderer draws from. Exposed so a caller can re-upload a changed
    /// texture in place via <see cref="TextureManager.Reload"/> (dev-time asset hot-reload, see
    /// <c>docs/asset-hot-reload.md</c>) — every quad already queued or submitted with that path
    /// picks up the change with no handle churn, since <see cref="RenderBatch"/> resolves the
    /// texture by path from this same instance on every flush.
    /// </summary>
    public TextureManager Textures => _textureManager;

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

    public void BeginFrame() => BeginFrame(clear: true);

    /// <inheritdoc />
    public void BeginFrame(bool clear)
    {
        // Window owns the viewport — it syncs on resize, so the renderer doesn't touch it here.
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        if (clear)
        {
            _gl.ClearColor(0f, 0f, 0f, 1f);
            _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        }

        _submissionQueue.Clear();

        CheckGlError();
    }

    /// <summary>
    /// Sets the view-projection matrix applied to quads submitted from now on. Defaults to
    /// <see cref="Matrix4x4.Identity"/>, which renders quads in NDC directly. If the matrix
    /// changes mid-frame, quads already queued are flushed first with the previous matrix.
    /// </summary>
    public void SetCamera(Matrix4x4 viewProjection)
    {
        _camera.Set(viewProjection, FlushQueuedQuads);
    }

    /// <inheritdoc />
    public Vector2 GetTextureSize(string path)
    {
        var texture = _textureManager.Get(path);
        return new Vector2(texture.Width, texture.Height);
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
        QuadVertexWriter.FromMatrix(in model, out var origin, out var xAxis, out var yAxis);
        _submissionQueue.Add(
            new QuadSubmission(
                _texturePaths.GetId(texturePath),
                origin,
                xAxis,
                yAxis,
                uvMin,
                uvMax,
                color
            )
        );
    }

    /// <inheritdoc />
    public void SubmitQuad(
        Vector2 center,
        Vector2 xAxis,
        Vector2 yAxis,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    ) =>
        _submissionQueue.Add(
            new QuadSubmission(
                _texturePaths.GetId(texturePath),
                new Vector3(center, 0f),
                new Vector3(xAxis, 0f),
                new Vector3(yAxis, 0f),
                uvMin,
                uvMax,
                color
            )
        );

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
    /// Flushes queued quads immediately while preserving submission order. Shader, VAO and
    /// VBO are bound once for the whole flush; only the texture changes between batches.
    /// </summary>
    public void FlushQueuedQuads()
    {
        var queue = CollectionsMarshal.AsSpan(_submissionQueue);
        if (queue.IsEmpty)
            return;

        _textureShader.Bind();
        _textureShader.SetUniformMatrix4("uViewProj", _camera.Current);
        _vao.Bind();
        _vbo.Bind();

        var startIndex = 0;
        while (startIndex < queue.Length)
        {
            var textureId = queue[startIndex].TextureId;
            var batchSize = 1;
            var limit = Math.Min(queue.Length - startIndex, MaxQuadsPerBatch);
            while (batchSize < limit && queue[startIndex + batchSize].TextureId == textureId)
            {
                batchSize++;
            }

            RenderBatch(textureId, queue.Slice(startIndex, batchSize));
            startIndex += batchSize;
        }

        _vao.Unbind();
        _textureShader.Unbind();
        _submissionQueue.Clear();
    }

    private void RenderBatch(int textureId, ReadOnlySpan<QuadSubmission> batch)
    {
        // The Texture object is stable across TextureManager.Reload (re-uploaded in place),
        // so caching it per id is safe and skips the per-batch path lookup.
        while (_textures.Count <= textureId)
            _textures.Add(null);
        var texture = _textures[textureId] ??= _textureManager.Get(
            _texturePaths.GetPath(textureId)
        );

        texture.Bind();
        FillVertexBuffer(batch);
        DrawBatch(batch.Length);
        texture.Unbind();
    }

    private void FillVertexBuffer(ReadOnlySpan<QuadSubmission> batch)
    {
        var vertices = _vertexBuffer.AsSpan();
        for (var q = 0; q < batch.Length; q++)
        {
            ref readonly var s = ref batch[q];
            QuadVertexWriter.Write(
                vertices[(q * QuadVertexWriter.FloatsPerQuad)..],
                s.Origin,
                s.XAxis,
                s.YAxis,
                s.UvMin,
                s.UvMax,
                s.Color
            );
        }
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
        int TextureId,
        Vector3 Origin,
        Vector3 XAxis,
        Vector3 YAxis,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color
    );
}

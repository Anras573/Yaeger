using System.Numerics;
using System.Runtime.InteropServices;
using Yaeger.Browser.Interop;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IRenderSurface"/> implementation that renders textured, tinted quads onto an
/// HTML5 canvas via a WebGL 2.0 context.  Sprites and sprite-sheet UV sub-regions are
/// fully supported.  Contiguous quads that share a texture path are coalesced into a single
/// WebGL draw call (up to 1 000 quads), matching the batching strategy of the desktop
/// <c>Renderer</c>.
/// </summary>
public sealed class BrowserRenderSurface(string canvasId) : IRenderSurface, IViewport, IDisposable
{
    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = 4;
    private const int FloatsPerVertex = 9; // pos(3) + uv(2) + color(4)

    private readonly List<QuadSubmission> _submissionQueue = [];
    private readonly TexturePathTable _texturePaths = new();
    private readonly CameraMatrixTracker _camera = new();

    // Scratch float buffer, filled per batch; _vertexBytes is its raw-byte view passed to JS.
    private readonly float[] _vertexBuffer = new float[
        MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex
    ];
    private readonly byte[] _vertexBytes = new byte[
        MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex * 4
    ];

    /// <summary>
    /// Initialises the WebGL context. Must be called once, after
    /// <c>JSHost.ImportAsync("yaeger-browser", …)</c> has completed.
    /// </summary>
    public void Initialize() => JsInterop.InitWebGL(canvasId);

    /// <summary>
    /// Colour the canvas is cleared to at the start of every frame (components in [0, 1],
    /// straight RGBA). Defaults to opaque black. An alpha below 1 lets the page behind the
    /// canvas show through, since the WebGL context is created with an alpha channel; note
    /// that the browser composites the canvas as premultiplied alpha while sprites blend with
    /// straight alpha, so semi-transparent sprites over a transparent clear can look washed out.
    /// </summary>
    public Vector4 ClearColor { get; set; } = new(0f, 0f, 0f, 1f);

    // Test seam: the real sink calls into JS, which is unavailable outside the browser.
    internal Action<Vector4> ClearFrameSink { get; set; } = JsInterop.ClearFrame;

    internal void ClearFrame() => ClearFrameSink(ClearColor);

    /// <summary>Canvas size in CSS pixels, as of the last <see cref="BeginFrame"/>.</summary>
    public Vector2 Size { get; private set; } = new(1f, 1f);

    /// <summary><c>devicePixelRatio</c>, as of the last <see cref="BeginFrame"/>.</summary>
    public float PixelRatio { get; private set; } = 1f;

    /// <summary>Re-reads the canvas size and device pixel ratio from the page.</summary>
    public void RefreshViewport()
    {
        var v = JsInterop.GetViewport();
        Size = new Vector2((float)v[0], (float)v[1]);
        PixelRatio = v[2] > 0 ? (float)v[2] : 1f;
    }

    /// <summary>
    /// Sampling for textures with no per-path override (default <see cref="TextureSampling.Default"/>,
    /// same as native). Set before textures start loading.
    /// </summary>
    public void SetDefaultSampling(TextureSampling sampling) =>
        JsInterop.SetDefaultTextureSampling((int)sampling.Filter, (int)sampling.Wrap);

    /// <summary>Overrides filtering/wrapping for one texture path; mirrors <c>TextureManager.SetSampling</c>.</summary>
    public void SetSampling(string texturePath, TextureSampling sampling) =>
        JsInterop.SetTextureSampling(texturePath, (int)sampling.Filter, (int)sampling.Wrap);

    /// <summary>
    /// Loads and uploads every texture in <paramref name="paths"/> concurrently so the first draw
    /// never shows the white placeholder. Reports completed/total in [0, 1] through
    /// <paramref name="progress"/>. Every path is attempted; if any fail, throws an
    /// <see cref="AggregateException"/> of <see cref="TextureLoadException"/>s afterwards.
    /// </summary>
    public async Task PreloadAsync(IEnumerable<string> paths, IProgress<float>? progress = null)
    {
        var distinct = paths.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        if (distinct.Count == 0)
        {
            progress?.Report(1f);
            return;
        }

        var done = 0;
        var failures = new List<Exception>();
        var tasks = distinct.Select(async path =>
        {
            try
            {
                await JsInterop.PreloadTexture(path);
            }
            catch (Exception e)
            {
                lock (failures)
                    failures.Add(new TextureLoadException(path, e.Message, e));
            }
            progress?.Report((float)Interlocked.Increment(ref done) / distinct.Count);
        });
        await Task.WhenAll(tasks);

        if (failures.Count > 0)
            throw new AggregateException("One or more textures failed to load.", failures);
    }

    /// <summary>True once <paramref name="path"/> is decoded and uploaded (empty/solid path is always ready).</summary>
    public bool IsReady(string path) => JsInterop.IsTextureReady(path);

    /// <summary>Pixel size of a ready texture, or <see cref="Vector2.Zero"/> if it is not ready.</summary>
    public Vector2 GetTextureSize(string path)
    {
        if (_textureSizes.TryGetValue(path, out var cached))
            return cached;

        var s = JsInterop.GetTextureSize(path);
        var size = new Vector2((float)s[0], (float)s[1]);
        // Sizes are fixed once loaded; only cache a real one so a not-ready texture is re-queried.
        if (size.X > 0f && size.Y > 0f)
            _textureSizes[path] = size;
        return size;
    }

    private readonly Dictionary<string, Vector2> _textureSizes = new();

    /// <summary>
    /// The error for a texture whose load failed (including lazy loads started by drawing),
    /// or <c>null</c> if it hasn't failed.
    /// </summary>
    public string? GetLoadError(string path) => JsInterop.GetTextureError(path);

    public void Dispose()
    {
        BrowserInputState.EndFrame();
        JsInterop.DisposeCanvas();
    }

    public void BeginFrame() => BeginFrame(clear: true);

    public void BeginFrame(bool clear)
    {
        RefreshViewport();
        if (clear)
            ClearFrame();
        _submissionQueue.Clear();
    }

    public void EndFrame()
    {
        FlushQueuedQuads();
    }

    public void FlushQueuedQuads()
    {
        var queue = CollectionsMarshal.AsSpan(_submissionQueue);
        if (queue.IsEmpty)
            return;

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

            RenderBatch(_texturePaths.GetPath(textureId), queue.Slice(startIndex, batchSize));
            startIndex += batchSize;
        }

        _submissionQueue.Clear();
    }

    public void SetCamera(Matrix4x4 viewProjection)
    {
        // Flushes queued quads with the previous matrix before the uniform changes.
        if (!_camera.Set(viewProjection, FlushQueuedQuads))
            return;

        var span = MemoryMarshal.CreateReadOnlySpan(ref viewProjection, 1);
        var bytes = new byte[64];
        MemoryMarshal.AsBytes(span).CopyTo(bytes);
        JsInterop.SetViewProjection(bytes);
    }

    public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
        SubmitQuad(transform, texturePath, Vector2.Zero, Vector2.One, color);

    public void SubmitQuad(
        Matrix4x4 transform,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    )
    {
        QuadVertexWriter.FromMatrix(in transform, out var origin, out var xAxis, out var yAxis);
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

    private void RenderBatch(string texturePath, ReadOnlySpan<QuadSubmission> batch)
    {
        FillVertexBuffer(batch);
        var byteCount = batch.Length * VerticesPerQuad * FloatsPerVertex * 4;
        Buffer.BlockCopy(_vertexBuffer, 0, _vertexBytes, 0, byteCount);
        JsInterop.DrawBatch(texturePath, _vertexBytes, batch.Length);
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

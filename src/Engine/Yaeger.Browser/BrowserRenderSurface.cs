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
public sealed class BrowserRenderSurface(string canvasId) : IRenderSurface, IDisposable
{
    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = 4;
    private const int FloatsPerVertex = 9; // pos(3) + uv(2) + color(4)

    private readonly List<QuadSubmission> _submissionQueue = [];

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

    public void Dispose()
    {
        BrowserInputState.EndFrame();
        JsInterop.DisposeCanvas();
    }

    public void BeginFrame()
    {
        BrowserInputState.BeginFrame();
        JsInterop.ClearFrame();
        _submissionQueue.Clear();
    }

    public void EndFrame()
    {
        FlushQueuedQuads();
        BrowserInputState.EndFrame();
    }

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

    public void SetCamera(Matrix4x4 viewProjection)
    {
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
    ) => _submissionQueue.Add(new QuadSubmission(texturePath, transform, uvMin, uvMax, color));

    private void RenderBatch(string texturePath, int startIndex, int batchSize)
    {
        FillVertexBuffer(startIndex, batchSize);
        var byteCount = batchSize * VerticesPerQuad * FloatsPerVertex * 4;
        Buffer.BlockCopy(_vertexBuffer, 0, _vertexBytes, 0, byteCount);
        JsInterop.DrawBatch(texturePath, _vertexBytes, batchSize);
    }

    private void FillVertexBuffer(int startIndex, int count)
    {
        // Corner order matches the quad winding baked into the JS index buffer: TR, BR, BL, TL.
        ReadOnlySpan<Vector3> basePositions =
        [
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        ];

        for (var q = 0; q < count; q++)
        {
            var sub = _submissionQueue[startIndex + q];

            ReadOnlySpan<Vector2> uvs =
            [
                new Vector2(sub.UvMax.X, sub.UvMax.Y), // TR
                new Vector2(sub.UvMax.X, sub.UvMin.Y), // BR
                new Vector2(sub.UvMin.X, sub.UvMin.Y), // BL
                new Vector2(sub.UvMin.X, sub.UvMax.Y), // TL
            ];

            var baseOffset = q * VerticesPerQuad * FloatsPerVertex;
            for (var c = 0; c < VerticesPerQuad; c++)
            {
                var pos = Vector3.Transform(basePositions[c], sub.Transform);
                var offset = baseOffset + c * FloatsPerVertex;
                _vertexBuffer[offset + 0] = pos.X;
                _vertexBuffer[offset + 1] = pos.Y;
                _vertexBuffer[offset + 2] = pos.Z;
                _vertexBuffer[offset + 3] = uvs[c].X;
                _vertexBuffer[offset + 4] = uvs[c].Y;
                _vertexBuffer[offset + 5] = sub.Color.X;
                _vertexBuffer[offset + 6] = sub.Color.Y;
                _vertexBuffer[offset + 7] = sub.Color.Z;
                _vertexBuffer[offset + 8] = sub.Color.W;
            }
        }
    }

    private readonly record struct QuadSubmission(
        string TexturePath,
        Matrix4x4 Transform,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color
    );
}

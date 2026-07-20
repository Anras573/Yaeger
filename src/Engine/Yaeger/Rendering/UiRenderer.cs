using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Yaeger.Graphics;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// Renders solid-colour rectangles for UI panels and button backgrounds.
/// Uses a texture-free colour-only shader so no assets are required.
/// Call <see cref="BeginFrame"/> once per frame with the current window size,
/// queue rectangles via <see cref="SubmitRect"/>, then call <see cref="EndFrame"/>.
/// </summary>
public class UiRenderer : IDisposable
{
    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = QuadIndexing.VerticesPerQuad;
    private const int IndicesPerQuad = QuadIndexing.IndicesPerQuad;
    private const int FloatsPerVertex = 6; // 2 NDC position + 4 RGBA color

    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load("Ui.vert");
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load("Ui.frag");

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao;
    private readonly Buffer<float> _vbo;
    private readonly Buffer<uint> _ebo;

    private readonly float[] _vertexBuffer = new float[
        MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex
    ];
    private int _quadCount;
    private Vector2 _windowSize;

    public UiRenderer(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _gl =
            window.Gl
            ?? throw new InvalidOperationException(
                "Window must have an initialized GL context before creating UiRenderer."
            );
        _shader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

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

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);
        _vbo.Bind();
        _ebo.Bind();

        unsafe
        {
            // position: 2 floats at offset 0
            _gl.VertexAttribPointer(
                0,
                2,
                VertexAttribPointerType.Float,
                false,
                FloatsPerVertex * sizeof(float),
                (void*)0
            );
            _gl.EnableVertexAttribArray(0);

            // color: 4 floats at offset 2
            _gl.VertexAttribPointer(
                1,
                4,
                VertexAttribPointerType.Float,
                false,
                FloatsPerVertex * sizeof(float),
                (void*)(2 * sizeof(float))
            );
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Clears the colour buffer. Call this at the start of the render callback when the
    /// UI is the only renderer in the scene (no game <c>Renderer</c> providing the clear).
    /// In a full game, skip this; let the game renderer call BeginFrame/Clear instead.
    /// </summary>
    public void Clear(Color backgroundColor)
    {
        var c = backgroundColor.ToVector4();
        _gl.ClearColor(c.X, c.Y, c.Z, c.W);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
    }

    /// <summary>
    /// Must be called once per frame before any <see cref="SubmitRect"/> calls.
    /// Enables alpha blending and captures the window size for pixel-to-NDC conversion.
    /// </summary>
    public void BeginFrame(Vector2 windowSize)
    {
        _windowSize = windowSize;
        _quadCount = 0;
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    /// <summary>
    /// Queues a filled rectangle in screen pixels (top-left origin, Y-down).
    /// Automatically flushes if the batch is full. Zero-size rects are skipped.
    /// </summary>
    public void SubmitRect(Vector2 position, Vector2 size, Color color)
    {
        if (size.X <= 0 || size.Y <= 0)
            return;

        if (_quadCount >= MaxQuadsPerBatch)
            Flush();

        // Convert screen pixels to NDC (Y flipped: pixel Y=0 is NDC Y=+1)
        var x1 = ToNdcX(position.X);
        var y1 = ToNdcY(position.Y);
        var x2 = ToNdcX(position.X + size.X);
        var y2 = ToNdcY(position.Y + size.Y);

        float r = color.R / 255.0f;
        float g = color.G / 255.0f;
        float b = color.B / 255.0f;
        float a = color.A / 255.0f;

        // Winding: TR(0), BR(1), BL(2), TL(3) — matches QuadIndexing order (0,1,3) + (1,2,3)
        var offset = _quadCount * VerticesPerQuad * FloatsPerVertex;

        // TR
        _vertexBuffer[offset++] = x2;
        _vertexBuffer[offset++] = y1;
        _vertexBuffer[offset++] = r;
        _vertexBuffer[offset++] = g;
        _vertexBuffer[offset++] = b;
        _vertexBuffer[offset++] = a;

        // BR
        _vertexBuffer[offset++] = x2;
        _vertexBuffer[offset++] = y2;
        _vertexBuffer[offset++] = r;
        _vertexBuffer[offset++] = g;
        _vertexBuffer[offset++] = b;
        _vertexBuffer[offset++] = a;

        // BL
        _vertexBuffer[offset++] = x1;
        _vertexBuffer[offset++] = y2;
        _vertexBuffer[offset++] = r;
        _vertexBuffer[offset++] = g;
        _vertexBuffer[offset++] = b;
        _vertexBuffer[offset++] = a;

        // TL
        _vertexBuffer[offset++] = x1;
        _vertexBuffer[offset++] = y1;
        _vertexBuffer[offset++] = r;
        _vertexBuffer[offset++] = g;
        _vertexBuffer[offset++] = b;
        _vertexBuffer[offset] = a;

        _quadCount++;
    }

    /// <summary>Flushes any queued rectangles to the GPU.</summary>
    public void EndFrame() => Flush();

    private float ToNdcX(float px) => _windowSize.X > 0 ? (px / _windowSize.X) * 2f - 1f : 0f;

    private float ToNdcY(float py) => _windowSize.Y > 0 ? 1f - (py / _windowSize.Y) * 2f : 0f;

    private unsafe void Flush()
    {
        if (_quadCount == 0)
            return;

        _shader.Bind();
        _gl.BindVertexArray(_vao);
        _vbo.Bind();

        int vertexCount = _quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* vertices = _vertexBuffer)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(vertexCount * sizeof(float)),
                vertices
            );
        }

        int indexCount = _quadCount * IndicesPerQuad;
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)indexCount,
            DrawElementsType.UnsignedInt,
            null
        );

        _gl.BindVertexArray(0);
        _shader.Unbind();

        CheckGlError(nameof(Flush));
        _quadCount = 0;
    }

    private void CheckGlError([CallerMemberName] string context = "")
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
            Console.WriteLine($"OpenGL error in UiRenderer.{context}: {error}");
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _vbo.Dispose();
        _ebo.Dispose();
        _shader.Dispose();
        GC.SuppressFinalize(this);
    }
}

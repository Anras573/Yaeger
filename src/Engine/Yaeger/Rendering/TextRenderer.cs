using System.Numerics;

using Silk.NET.OpenGL;

using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// Specialized renderer for text using glyph atlases and batch rendering.
/// </summary>
public class TextRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Shader _textShader;
    private readonly Dictionary<Font.Font, GlyphAtlas> _glyphAtlases = new();
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;

    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int FloatsPerVertex = 9; // 3 position + 2 texcoord + 4 color

    private readonly float[] _vertexBuffer;
    private readonly uint[] _indexBuffer;
    private int _quadCount;

    private const string VertexShaderSource = """
                                              #version 330 core
                                              layout(location = 0) in vec3 aPosition;
                                              layout(location = 1) in vec2 aTexCoord;
                                              layout(location = 2) in vec4 aColor;
                                              
                                              out vec2 vTexCoord;
                                              out vec4 vColor;
                                              
                                              void main()
                                              {
                                                  gl_Position = vec4(aPosition, 1.0);
                                                  vTexCoord = aTexCoord;
                                                  vColor = aColor;
                                              }
                                              """;

    private const string FragmentShaderSource = """
                                                #version 330 core
                                                in vec2 vTexCoord;
                                                in vec4 vColor;
                                                out vec4 FragColor;
                                                
                                                uniform sampler2D uTexture;
                                                
                                                void main()
                                                {
                                                    float alpha = texture(uTexture, vTexCoord).r;
                                                    FragColor = vec4(vColor.rgb, vColor.a * alpha);
                                                }
                                                """;

    public unsafe TextRenderer(Window window)
    {
        _gl = window.Gl ?? throw new ArgumentNullException(nameof(window));
        _textShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vertexBuffer = new float[MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex];
        _indexBuffer = new uint[MaxQuadsPerBatch * IndicesPerQuad];

        // Generate static indices
        for (uint i = 0; i < MaxQuadsPerBatch; i++)
        {
            uint offset = i * VerticesPerQuad;
            uint indexOffset = i * IndicesPerQuad;

            _indexBuffer[indexOffset + 0] = offset + 0;
            _indexBuffer[indexOffset + 1] = offset + 1;
            _indexBuffer[indexOffset + 2] = offset + 3;
            _indexBuffer[indexOffset + 3] = offset + 1;
            _indexBuffer[indexOffset + 4] = offset + 2;
            _indexBuffer[indexOffset + 5] = offset + 3;
        }

        // Create VAO
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Create VBO
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer,
            (nuint)(_vertexBuffer.Length * sizeof(float)),
            null,
            BufferUsageARB.DynamicDraw);

        // Create EBO
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* indices = _indexBuffer)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(_indexBuffer.Length * sizeof(uint)),
                indices,
                BufferUsageARB.StaticDraw);
        }

        // Setup vertex attributes
        // Position (3 floats)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false,
            FloatsPerVertex * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // TexCoord (2 floats)
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false,
            FloatsPerVertex * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        // Color (4 floats)
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false,
            FloatsPerVertex * sizeof(float), (void*)(5 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindVertexArray(0);

        // Enable blending for text rendering
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Console.WriteLine($"TextRenderer initialized with max {MaxQuadsPerBatch} glyphs per batch");
    }

    /// <summary>
    /// Gets or creates a glyph atlas for the specified font.
    /// </summary>
    public GlyphAtlas GetOrCreateAtlas(Font.Font font, int fontSize = 48)
    {
        if (_glyphAtlases.TryGetValue(font, out var atlas))
        {
            return atlas;
        }

        atlas = new GlyphAtlas(_gl, font, fontSize);
        _glyphAtlases[font] = atlas;
        return atlas;
    }

    /// <summary>
    /// Renders text at the specified position with the given transform.
    /// </summary>
    public void DrawText(string text, Matrix4x4 transform, Font.Font font, int fontSize, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var atlas = GetOrCreateAtlas(font, fontSize);
        atlas.AddGlyphsForText(text);

        var shaper = new TextShaper(font);
        var glyphs = shaper.Shape(text);

        _quadCount = 0;
        float x = 0;
        float y = 0;

        foreach (var glyph in glyphs)
        {
            var atlasGlyph = atlas.GetGlyph(glyph.Codepoint);
            if (!atlasGlyph.HasValue)
                continue;

            var ag = atlasGlyph.Value;

            // Calculate glyph position
            float xpos = x + (glyph.XOffset / 64.0f) + ag.Bearing.X;
            float ypos = y + (glyph.YOffset / 64.0f) - (ag.Size.Y - ag.Bearing.Y);
            float w = ag.Size.X;
            float h = ag.Size.Y;

            // Create quad vertices for this glyph
            AddGlyphQuad(transform, xpos, ypos, w, h, ag.TexCoordMin, ag.TexCoordMax, color);

            // Advance cursor
            x += glyph.XAdvance / 64.0f;
            y += glyph.YAdvance / 64.0f;

            // If batch is full, render it
            if (_quadCount >= MaxQuadsPerBatch)
            {
                RenderBatch(atlas.TextureId);
                _quadCount = 0;
            }
        }

        // Render remaining quads
        if (_quadCount > 0)
        {
            RenderBatch(atlas.TextureId);
        }
    }

    private void AddGlyphQuad(Matrix4x4 transform, float x, float y, float w, float h,
        Vector2 texMin, Vector2 texMax, Color color)
    {
        // Normalize color to 0-1 range
        float r = color.R / 255.0f;
        float g = color.G / 255.0f;
        float b = color.B / 255.0f;
        float a = color.A / 255.0f;

        // Base quad positions (in world space, will be normalized later)
        ReadOnlySpan<Vector3> positions = stackalloc Vector3[]
        {
            new Vector3(x + w, y + h, 0.0f), // top-right
            new Vector3(x + w, y,     0.0f), // bottom-right
            new Vector3(x,     y,     0.0f), // bottom-left
            new Vector3(x,     y + h, 0.0f)  // top-left
        };

        ReadOnlySpan<Vector2> texCoords = stackalloc Vector2[]
        {
            new Vector2(texMax.X, texMax.Y),
            new Vector2(texMax.X, texMin.Y),
            new Vector2(texMin.X, texMin.Y),
            new Vector2(texMin.X, texMax.Y)
        };

        int vertexOffset = _quadCount * VerticesPerQuad * FloatsPerVertex;

        for (int v = 0; v < VerticesPerQuad; v++)
        {
            var transformedPos = Vector3.Transform(positions[v], transform);
            int idx = vertexOffset + v * FloatsPerVertex;

            _vertexBuffer[idx + 0] = transformedPos.X;
            _vertexBuffer[idx + 1] = transformedPos.Y;
            _vertexBuffer[idx + 2] = transformedPos.Z;
            _vertexBuffer[idx + 3] = texCoords[v].X;
            _vertexBuffer[idx + 4] = texCoords[v].Y;
            _vertexBuffer[idx + 5] = r;
            _vertexBuffer[idx + 6] = g;
            _vertexBuffer[idx + 7] = b;
            _vertexBuffer[idx + 8] = a;
        }

        _quadCount++;
    }

    private unsafe void RenderBatch(uint textureId)
    {
        if (_quadCount == 0)
            return;

        _textShader.Bind();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, textureId);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        int vertexCount = _quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* vertices = _vertexBuffer)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                (nuint)(vertexCount * sizeof(float)), vertices);
        }

        int indexCount = _quadCount * IndicesPerQuad;
        _gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount,
            DrawElementsType.UnsignedInt, null);

        _gl.BindVertexArray(0);
        _textShader.Unbind();
    }

    public void Dispose()
    {
        foreach (var atlas in _glyphAtlases.Values)
        {
            atlas.Dispose();
        }
        _glyphAtlases.Clear();

        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _textShader.Dispose();

        GC.SuppressFinalize(this);
    }
}
using System.Numerics;
using System.Runtime.CompilerServices;

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
    private readonly FontVertexArray _vao;
    private readonly Buffer<float> _vbo;
    private readonly Buffer<uint> _ebo;

    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int FloatsPerVertex = 9; // 3 position + 2 texcoord + 4 color

    private readonly float[] _vertexBuffer;
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

    public TextRenderer(Window window)
    {
        _gl = window.Gl ?? throw new ArgumentNullException(nameof(window));
        _textShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vertexBuffer = new float[MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex];
        uint[] indexBuffer = new uint[MaxQuadsPerBatch * IndicesPerQuad];

        // Generate static indices
        for (uint i = 0; i < MaxQuadsPerBatch; i++)
        {
            uint offset = i * VerticesPerQuad;
            uint indexOffset = i * IndicesPerQuad;

            indexBuffer[indexOffset + 0] = offset + 0;
            indexBuffer[indexOffset + 1] = offset + 1;
            indexBuffer[indexOffset + 2] = offset + 3;
            indexBuffer[indexOffset + 3] = offset + 1;
            indexBuffer[indexOffset + 4] = offset + 2;
            indexBuffer[indexOffset + 5] = offset + 3;
        }

        // Create VBO
        _vbo = new Buffer<float>(_gl, _vertexBuffer, BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);

        // Create EBO
        _ebo = new Buffer<uint>(_gl, indexBuffer, BufferTargetARB.ElementArrayBuffer);

        // Create VAO
        _vao = new FontVertexArray(_gl, _vbo, _ebo);
        _vao.Unbind();

        // Enable blending for text rendering
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Console.WriteLine($"TextRenderer initialized with max {MaxQuadsPerBatch} glyphs per batch");
    }

    /// <summary>
    /// Gets or creates a glyph atlas for the specified font.
    /// </summary>
    private GlyphAtlas GetOrCreateAtlas(Font.Font font, int fontSize = 48)
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
                RenderBatch(atlas);
                _quadCount = 0;
            }
        }

        // Render remaining quads
        if (_quadCount > 0)
        {
            RenderBatch(atlas);
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

    private unsafe void RenderBatch(GlyphAtlas atlas)
    {
        if (_quadCount == 0)
            return;

        _textShader.Bind();
        atlas.BindTexture();
        _vao.Bind();
        _vbo.Bind();

        int vertexCount = _quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* vertices = _vertexBuffer)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                (nuint)(vertexCount * sizeof(float)), vertices);
        }

        int indexCount = _quadCount * IndicesPerQuad;
        _gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount, DrawElementsType.UnsignedInt, null);

        _vao.Unbind();
        _textShader.Unbind();

        CheckGlError(nameof(TextRenderer));
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
        foreach (var atlas in _glyphAtlases.Values)
        {
            atlas.Dispose();
        }
        _glyphAtlases.Clear();

        _vao.Dispose();
        _vbo.Dispose();
        _ebo.Dispose();
        _textShader.Dispose();

        GC.SuppressFinalize(this);
    }
}
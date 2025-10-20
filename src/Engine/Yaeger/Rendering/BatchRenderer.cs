using System.Numerics;

using Silk.NET.OpenGL;

using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// A batch renderer that groups draw calls by texture to reduce state changes.
/// This is particularly useful for rendering text and sprites efficiently.
/// </summary>
public class BatchRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly TextureManager _textureManager;
    private readonly Shader _batchShader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;

    private const int MaxQuadsPerBatch = 1000;
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int FloatsPerVertex = 5; // 3 position + 2 texcoord

    private readonly float[] _vertexBuffer;
    private readonly uint[] _indexBuffer;
    private int _quadCount;

    private const string VertexShaderSource = """
                                              #version 330 core
                                              layout(location = 0) in vec3 aPosition;
                                              layout(location = 1) in vec2 aTexCoord;
                                              
                                              out vec2 vTexCoord;
                                              
                                              void main()
                                              {
                                                  gl_Position = vec4(aPosition, 1.0);
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

    private readonly Dictionary<string, List<Matrix4x4>> _batchQueue;

    public unsafe BatchRenderer(Window window)
    {
        _gl = window.Gl;
        _textureManager = new TextureManager(_gl);
        _batchShader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);

        _vertexBuffer = new float[MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex];
        _indexBuffer = new uint[MaxQuadsPerBatch * IndicesPerQuad];
        _batchQueue = new Dictionary<string, List<Matrix4x4>>();

        // Generate static indices (pattern repeats for each quad)
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
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(_vertexBuffer.Length * sizeof(float)),
                null,
                BufferUsageARB.DynamicDraw);
        }

        // Create EBO
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        unsafe
        {
            fixed (uint* indices = _indexBuffer)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                    (nuint)(_indexBuffer.Length * sizeof(uint)),
                    indices,
                    BufferUsageARB.StaticDraw);
            }
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

        _gl.BindVertexArray(0);

        Console.WriteLine($"BatchRenderer initialized with max {MaxQuadsPerBatch} quads per batch");
    }

    public void BeginFrame()
    {
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        _batchQueue.Clear();
    }

    /// <summary>
    /// Submit a quad to be batched. Quads are grouped by texture and rendered together.
    /// </summary>
    public void SubmitQuad(Matrix4x4 transform, string texturePath)
    {
        if (!_batchQueue.ContainsKey(texturePath))
        {
            _batchQueue[texturePath] = new List<Matrix4x4>();
        }
        _batchQueue[texturePath].Add(transform);
    }

    /// <summary>
    /// Render all batched quads. This should be called once per frame after all SubmitQuad calls.
    /// </summary>
    public void EndFrame()
    {
        foreach (var (texturePath, transforms) in _batchQueue)
        {
            RenderBatch(texturePath, transforms);
        }
    }

    private void RenderBatch(string texturePath, List<Matrix4x4> transforms)
    {
        var texture = _textureManager.Get(texturePath);
        _batchShader.Bind();
        texture.Bind();

        // Process transforms in batches
        for (int i = 0; i < transforms.Count; i += MaxQuadsPerBatch)
        {
            int batchSize = Math.Min(MaxQuadsPerBatch, transforms.Count - i);
            FillVertexBuffer(transforms, i, batchSize);
            DrawBatch(batchSize);
        }

        texture.Unbind();
        _batchShader.Unbind();
    }

    private void FillVertexBuffer(List<Matrix4x4> transforms, int startIndex, int count)
    {
        _quadCount = count;

        // Base quad vertices (unit square centered at origin)
        ReadOnlySpan<Vector3> basePositions = stackalloc Vector3[]
        {
            new Vector3( 0.5f,  0.5f, 0.0f),
            new Vector3( 0.5f, -0.5f, 0.0f),
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3(-0.5f,  0.5f, 0.0f)
        };

        ReadOnlySpan<Vector2> texCoords = stackalloc Vector2[]
        {
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f)
        };

        for (int i = 0; i < count; i++)
        {
            var transform = transforms[startIndex + i];
            int vertexOffset = i * VerticesPerQuad * FloatsPerVertex;

            for (int v = 0; v < VerticesPerQuad; v++)
            {
                var transformedPos = Vector3.Transform(basePositions[v], transform);
                int idx = vertexOffset + v * FloatsPerVertex;

                _vertexBuffer[idx + 0] = transformedPos.X;
                _vertexBuffer[idx + 1] = transformedPos.Y;
                _vertexBuffer[idx + 2] = transformedPos.Z;
                _vertexBuffer[idx + 3] = texCoords[v].X;
                _vertexBuffer[idx + 4] = texCoords[v].Y;
            }
        }
    }

    private unsafe void DrawBatch(int quadCount)
    {
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        int vertexCount = quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* vertices = _vertexBuffer)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                (nuint)(vertexCount * sizeof(float)), vertices);
        }

        int indexCount = quadCount * IndicesPerQuad;
        _gl.DrawElements(PrimitiveType.Triangles, (uint)indexCount,
            DrawElementsType.UnsignedInt, null);

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _batchShader.Dispose();
    }
}
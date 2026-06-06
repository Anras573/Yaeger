using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Renders a cubemap skybox around the scene using a rotation-only view matrix so the sky
/// appears infinitely distant. Designed to be called inside the depth-testing window
/// (after <see cref="Renderer3D.BeginFrame3D"/> and before <see cref="Renderer3D.EndFrame3D"/>),
/// using <c>LEQUAL</c> depth so the skybox is drawn behind all geometry already in the buffer.
/// </summary>
public sealed class SkyboxRenderer : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;

        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vTexCoords;

        void main() {
            vTexCoords  = aPosition;
            vec4 pos    = uProjection * uView * vec4(aPosition, 1.0);
            gl_Position = pos.xyww;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in  vec3 vTexCoords;
        out vec4 FragColor;

        uniform samplerCube uSkybox;

        void main() {
            FragColor = texture(uSkybox, vTexCoords);
        }
        """;

    // Unit cube — vertex positions are also the cubemap sample directions.
    private static readonly float[] Vertices =
    [
        -1f,  1f, -1f,  -1f, -1f, -1f,   1f, -1f, -1f,
         1f, -1f, -1f,   1f,  1f, -1f,  -1f,  1f, -1f,

        -1f, -1f,  1f,  -1f, -1f, -1f,  -1f,  1f, -1f,
        -1f,  1f, -1f,  -1f,  1f,  1f,  -1f, -1f,  1f,

         1f, -1f, -1f,   1f, -1f,  1f,   1f,  1f,  1f,
         1f,  1f,  1f,   1f,  1f, -1f,   1f, -1f, -1f,

        -1f, -1f,  1f,  -1f,  1f,  1f,   1f,  1f,  1f,
         1f,  1f,  1f,   1f, -1f,  1f,  -1f, -1f,  1f,

        -1f,  1f, -1f,   1f,  1f, -1f,   1f,  1f,  1f,
         1f,  1f,  1f,  -1f,  1f,  1f,  -1f,  1f, -1f,

        -1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f, -1f,
         1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f,  1f,
    ];

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao;
    private readonly uint _vbo;

    public unsafe SkyboxRenderer(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = Vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(Vertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw
            );
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws the skybox using a rotation-only view matrix derived from <paramref name="view"/>.
    /// Must be called while depth testing is active (inside a <see cref="Renderer3D.BeginFrame3D"/>
    /// / <see cref="Renderer3D.EndFrame3D"/> block).
    /// </summary>
    public void Draw(CubemapTexture cubemap, Matrix4x4 view, Matrix4x4 projection)
    {
        // Strip translation so the skybox stays centred on the camera.
        var rotationOnly = view;
        rotationOnly.M41 = 0f;
        rotationOnly.M42 = 0f;
        rotationOnly.M43 = 0f;

        // LEQUAL so the skybox (depth = 1.0 after pos.xyww) renders behind all geometry.
        _gl.DepthFunc(DepthFunction.Lequal);

        _shader.Bind();
        _shader.SetUniformMatrix4("uView", rotationOnly);
        _shader.SetUniformMatrix4("uProjection", projection);
        _shader.SetUniformInt("uSkybox", 0);

        cubemap.Bind(TextureUnit.Texture0);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
        _gl.BindVertexArray(0);

        cubemap.Unbind(TextureUnit.Texture0);
        _shader.Unbind();

        _gl.DepthFunc(DepthFunction.Less);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
    }
}

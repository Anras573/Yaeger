using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders 3D meshes with MVP transforms, depth testing, and back-face culling.
/// Independent of the 2D <see cref="Renderer"/> pipeline.
/// </summary>
public sealed class Renderer3D : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;

        uniform mat4 uModel;
        uniform mat4 uViewProj;
        uniform mat3 uNormalMatrix;

        out vec3 vNormal;
        out vec2 vTexCoord;
        out vec3 vFragPos;

        void main() {
            vec4 worldPos = uModel * vec4(aPosition, 1.0);
            vFragPos  = worldPos.xyz;
            vNormal   = uNormalMatrix * aNormal;
            vTexCoord = aTexCoord;
            gl_Position = uViewProj * worldPos;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in  vec3 vNormal;
        in  vec2 vTexCoord;
        in  vec3 vFragPos;
        out vec4 FragColor;

        uniform sampler2D uDiffuse;
        uniform vec4      uDiffuseColor;

        void main() {
            // Guard against degenerate inputs; keeps vNormal/vFragPos (and therefore
            // uNormalMatrix) active without affecting the unlit output. Lighting in #78.
            if (any(isnan(vNormal)) || any(isnan(vFragPos))) discard;
            FragColor = texture(uDiffuse, vTexCoord) * uDiffuseColor;
        }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _defaultTexture;

    public Renderer3D(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _defaultTexture = CreateWhiteTexture();
    }

    /// <summary>
    /// Enables depth testing and back-face culling, and clears the depth buffer.
    /// Call once at the start of the 3D pass each frame.
    /// </summary>
    public void BeginFrame3D()
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
    }

    /// <summary>
    /// Disables depth testing and back-face culling so the 2D pipeline is unaffected.
    /// Call once at the end of the 3D pass each frame.
    /// </summary>
    public void EndFrame3D()
    {
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
    }

    /// <summary>Draws a single mesh with the supplied transform and material.</summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    )
    {
        _shader.Bind();

        _shader.SetUniformMatrix4("uModel", model);
        _shader.SetUniformMatrix4("uViewProj", viewProj);

        if (!Matrix4x4.Invert(model, out var invModel))
            invModel = Matrix4x4.Identity;
        _shader.SetUniformMatrix3("uNormalMatrix", Matrix4x4.Transpose(invModel));

        _shader.SetUniformVec4("uDiffuseColor", material.Diffuse.ToVector4());

        if (!string.IsNullOrEmpty(material.DiffuseTexturePath))
            textures.Get(material.DiffuseTexturePath).Bind(TextureUnit.Texture0);
        else
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        }

        mesh.Draw();

        _shader.Unbind();
    }

    private unsafe uint CreateWhiteTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] white = [255, 255, 255, 255];
        fixed (byte* ptr = white)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                1,
                1,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest
        );
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteTexture(_defaultTexture);
    }
}

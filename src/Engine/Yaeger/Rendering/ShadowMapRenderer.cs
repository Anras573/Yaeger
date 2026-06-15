using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders scene depth from a directional light's point of view into an off-screen depth texture
/// (the "shadow map"). The lighting pass (<see cref="Renderer3D"/>) samples this texture to decide
/// which fragments are occluded from the light and should be darkened.
///
/// Usage each frame, before the lighting pass:
/// <code>
/// shadowMap.BeginPass(light, sceneCenter);
/// foreach (var (mesh, model) in casters) shadowMap.Draw(mesh, model);
/// shadowMap.EndPass(width, height);
/// renderer3D.SetShadowMap(shadowMap.LightSpaceMatrix, shadowMap.DepthTexture, bias, pcf);
/// </code>
/// </summary>
public sealed class ShadowMapRenderer : IDisposable
{
    // Depth-only pass: transform vertices into light clip space; no fragment output is needed, the
    // depth buffer is captured automatically.
    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;

        uniform mat4 uLightSpace;
        uniform mat4 uModel;

        void main() {
            gl_Position = uLightSpace * uModel * vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        void main() { }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _fbo;
    private readonly uint _depthTexture;
    private readonly int _resolution;

    /// <summary>The settings the renderer was constructed with.</summary>
    public ShadowSettings Settings { get; }

    /// <summary>Handle of the depth texture written during the shadow pass.</summary>
    public uint DepthTexture => _depthTexture;

    /// <summary>
    /// The light-space view-projection computed by the most recent <see cref="BeginPass"/> call.
    /// Upload this to <see cref="Renderer3D.SetShadowMap"/> so the lighting pass projects each
    /// fragment into the same space.
    /// </summary>
    public Matrix4x4 LightSpaceMatrix { get; private set; } = Matrix4x4.Identity;

    public ShadowMapRenderer(GL gl, ShadowSettings settings)
    {
        _gl = gl;
        Settings = settings;
        _resolution = Math.Max(settings.MapResolution, 1);
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _depthTexture = CreateDepthTexture(_resolution);
        _fbo = CreateFramebuffer(_depthTexture);
    }

    /// <summary>
    /// Computes the orthographic light-space view-projection that frames <paramref name="sceneCenter"/>
    /// from the directional light's direction. The light's eye is placed back along its (toward-the-
    /// light) direction so the centre sits midway between the near and far planes.
    /// </summary>
    public static Matrix4x4 ComputeLightSpaceMatrix(
        DirectionalLight light,
        Vector3 sceneCenter,
        ShadowSettings settings
    )
    {
        var lenSq = light.Direction.LengthSquared();
        var dir =
            float.IsFinite(lenSq) && lenSq > 0f
                ? Vector3.Normalize(light.Direction)
                : Vector3.UnitY;

        var near = settings.NearPlane > 0f ? settings.NearPlane : 0.1f;
        var far = settings.FarPlane > near ? settings.FarPlane : near + 1f;
        var size = settings.OrthographicSize > 0f ? settings.OrthographicSize : 1f;

        var distance = (near + far) * 0.5f;
        var eye = sceneCenter + dir * distance;

        // Pick an up vector that isn't (near-)parallel to the view direction so the look-at stays
        // well-defined for top-down lights.
        var up = MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

        var view = Matrix4x4.CreateLookAt(eye, sceneCenter, up);
        var projection = Matrix4x4.CreateOrthographicOffCenter(-size, size, -size, size, near, far);
        return view * projection;
    }

    /// <summary>
    /// Binds the depth framebuffer, sets the shadow-map viewport, clears depth, and uploads the
    /// light-space transform. Issue <see cref="Draw"/> calls for every shadow caster afterwards.
    /// </summary>
    public void BeginPass(DirectionalLight light, Vector3 sceneCenter)
    {
        LightSpaceMatrix = ComputeLightSpaceMatrix(light, sceneCenter, Settings);

        var resolution = (uint)_resolution;
        _gl.Viewport(0, 0, resolution, resolution);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Clear((uint)ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        // Render both faces into the depth map so single-sided geometry (walls, quads) still casts.
        _gl.Disable(EnableCap.CullFace);

        _shader.Bind();
        _shader.SetUniformMatrix4("uLightSpace", LightSpaceMatrix);
    }

    /// <summary>Renders a single shadow caster into the depth map. Call between Begin/End.</summary>
    public void Draw(GpuMesh mesh, Matrix4x4 model)
    {
        _shader.SetUniformMatrix4("uModel", model);
        mesh.Draw();
    }

    /// <summary>
    /// Restores the default framebuffer and the supplied viewport (the window's drawable size) so
    /// the subsequent lighting pass renders to the screen as usual.
    /// </summary>
    public void EndPass(int viewportWidth, int viewportHeight)
    {
        _shader.Unbind();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Math.Max(viewportWidth, 1), (uint)Math.Max(viewportHeight, 1));

        // Restore the engine's default 3D state changed in BeginPass so a caller that doesn't
        // immediately re-establish it (BeginFrame3D does) isn't left with culling disabled.
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.DepthFunc(DepthFunction.Less);
    }

    private unsafe uint CreateDepthTexture(int resolution)
    {
        // Select a known unit before binding so this (and the matching unbind below) doesn't
        // disturb whatever texture the caller had bound on an arbitrary active unit.
        _gl.ActiveTexture(TextureUnit.Texture0);
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            (int)InternalFormat.DepthComponent24,
            (uint)resolution,
            (uint)resolution,
            0,
            PixelFormat.DepthComponent,
            // Data is null, so this type only labels the (absent) source pixels; pair it with the
            // sized DepthComponent24 internal format's conventional integer type.
            PixelType.UnsignedInt,
            null
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Nearest
        );
        // Clamp to a white (depth 1.0) border so fragments that fall outside the light's frustum
        // sample "fully lit" rather than wrapping into a neighbouring shadow.
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToBorder
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToBorder
        );
        float* border = stackalloc float[] { 1f, 1f, 1f, 1f };
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, border);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    private uint CreateFramebuffer(uint depthTexture)
    {
        var fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D,
            depthTexture,
            0
        );
        // Depth-only target: no colour buffer is drawn to or read from.
        _gl.DrawBuffer(DrawBufferMode.None);
        _gl.ReadBuffer(ReadBufferMode.None);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Shadow map framebuffer incomplete: {status}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return fbo;
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteTexture(_depthTexture);
    }
}

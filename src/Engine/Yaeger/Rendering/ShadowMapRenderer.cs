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
    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load("ShadowMap.vert");
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "ShadowMap.frag"
    );

    /// <summary>Maximum number of bones the shadow shader's skinning palette can hold (matches Renderer3D.MaxBones and MAX_BONES in ShadowMap.vert).</summary>
    public const int MaxBones = 128;

    // Binding point linking the "Bones" uniform block to this renderer's own bone-matrix UBO. Distinct
    // from Renderer3D's own binding point so the two renderers' bone data never depend on draw order.
    private const uint BoneBlockBinding = 1;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _fbo;
    private readonly uint _depthTexture;
    private readonly uint _boneUbo;
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

    /// <summary>
    /// Number of real GL draw calls issued into the shadow map since the most recent
    /// <see cref="BeginPass"/>. Mirrors <see cref="Renderer3D.DrawCallCount"/> for the depth pre-pass.
    /// </summary>
    public int DrawCallCount { get; private set; }

    private InstanceData[]? _instanceScratch;

    public ShadowMapRenderer(GL gl, ShadowSettings settings)
    {
        _gl = gl;
        Settings = settings;
        _resolution = Math.Max(settings.MapResolution, 1);
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _depthTexture = CreateDepthTexture(_resolution);
        _fbo = CreateFramebuffer(_depthTexture);
        _boneUbo = CreateBoneUbo();
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
        DrawCallCount = 0;

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

    /// <summary>Renders a single static shadow caster into the depth map. Call between Begin/End.</summary>
    public void Draw(GpuMesh mesh, Matrix4x4 model)
    {
        _shader.SetUniformInt("uSkinned", 0);
        _shader.SetUniformInt("uInstanced", 0);
        _shader.SetUniformMatrix4("uModel", model);
        mesh.Draw();
        DrawCallCount++;
    }

    /// <summary>
    /// Renders a single skinned shadow caster into the depth map, uploading <paramref name="bonePalette"/>
    /// to this renderer's own bone-matrix UBO and enabling GPU skinning in the shadow vertex shader —
    /// mirrors <see cref="Renderer3D.Draw(GpuMesh, Matrix4x4, Matrix4x4, Material3D, TextureManager, ReadOnlySpan{Matrix4x4})"/>.
    /// Call between Begin/End. Skinned casters always take this immediate per-entity path, never the
    /// instanced one below — bone palettes are per-entity state.
    /// </summary>
    public void Draw(GpuMesh mesh, Matrix4x4 model, ReadOnlySpan<Matrix4x4> bonePalette)
    {
        SetBoneMatrices(bonePalette);
        _shader.SetUniformInt("uSkinned", 1);
        _shader.SetUniformInt("uInstanced", 0);
        _shader.SetUniformMatrix4("uModel", model);
        mesh.Draw();
        DrawCallCount++;
    }

    /// <summary>
    /// Renders <paramref name="models"/> copies of <paramref name="mesh"/> into the depth map in a
    /// single instanced draw call. Call between Begin/End. No-op for an empty span.
    /// </summary>
    public void DrawInstanced(GpuMesh mesh, ReadOnlySpan<Matrix4x4> models)
    {
        if (models.IsEmpty)
            return;

        _shader.SetUniformInt("uSkinned", 0);
        _shader.SetUniformInt("uInstanced", 1);

        if (_instanceScratch == null || _instanceScratch.Length < models.Length)
            _instanceScratch = new InstanceData[Math.Max(models.Length, 64)];

        for (var i = 0; i < models.Length; i++)
            _instanceScratch[i] = new InstanceData(models[i]);

        mesh.DrawInstanced(_instanceScratch.AsSpan(0, models.Length));
        DrawCallCount++;
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
        // Normalise to a known unit (Texture0) before binding so the texture setup lands on a
        // predictable unit — and leaves unit 0 cleared — instead of mutating whatever unit the
        // caller happened to leave active. Mirrors how Renderer3D settles on unit 0 after its
        // own texture setup; construction runs during scene init, so no live binding is lost.
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

    // Allocates the bone-matrix uniform buffer (MaxBones mat4s) and links it to the shadow shader's
    // "Bones" block via this renderer's own binding point. Mirrors Renderer3D.CreateBoneUbo.
    private unsafe uint CreateBoneUbo()
    {
        var ubo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, ubo);
        _gl.BufferData(
            BufferTargetARB.UniformBuffer,
            (nuint)(MaxBones * sizeof(Matrix4x4)),
            null,
            BufferUsageARB.DynamicDraw
        );
        _gl.BindBufferBase(BufferTargetARB.UniformBuffer, BoneBlockBinding, ubo);
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
        _shader.BindUniformBlock("Bones", BoneBlockBinding);
        return ubo;
    }

    // Uploads a skinning matrix palette to the bone UBO. At most MaxBones matrices are used; extras
    // are ignored. Mirrors Renderer3D.SetBoneMatrices.
    private unsafe void SetBoneMatrices(ReadOnlySpan<Matrix4x4> palette)
    {
        var count = Math.Min(palette.Length, MaxBones);
        if (count <= 0)
            return;

        _gl.BindBuffer(BufferTargetARB.UniformBuffer, _boneUbo);
        fixed (Matrix4x4* ptr = palette)
        {
            _gl.BufferSubData(
                BufferTargetARB.UniformBuffer,
                0,
                (nuint)(count * sizeof(Matrix4x4)),
                ptr
            );
        }
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteTexture(_depthTexture);
        _gl.DeleteBuffer(_boneUbo);
    }
}

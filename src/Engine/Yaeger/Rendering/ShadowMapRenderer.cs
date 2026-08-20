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

    /// <summary>
    /// Shadow strength for the light of the most recent <see cref="BeginPass"/>, in <c>[0, 1]</c> —
    /// pass it to <see cref="Renderer3D.SetShadowMap"/> so a light near the horizon fades its
    /// shadows out. See <see cref="ComputeShadowStrength"/>.
    /// </summary>
    public float ShadowStrength { get; private set; } = 1f;

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
    ) => ComputeLightSpaceMatrix(light, sceneCenter, sceneRadius: 0f, settings);

    /// <summary>
    /// Computes the light-space view-projection, optionally fitting it to a bounding sphere rather
    /// than to <see cref="ShadowSettings.OrthographicSize"/>.
    /// </summary>
    /// <param name="sceneRadius">
    /// Radius of the sphere bounding the shadow casters. Used only when
    /// <see cref="ShadowSettings.AutoFit"/> is set; a non-positive radius falls back to the
    /// configured extent, so an empty scene is handled without a degenerate frustum.
    /// </param>
    /// <remarks>
    /// The fitted frustum frames the whole sphere from wherever the light is, so it is independent
    /// of the light's angle — the property that keeps a sun's shadows inside the map from sunrise
    /// through noon, where a fixed extent only holds at the angle it was tuned for.
    /// </remarks>
    public static Matrix4x4 ComputeLightSpaceMatrix(
        DirectionalLight light,
        Vector3 sceneCenter,
        float sceneRadius,
        ShadowSettings settings
    )
    {
        var dir = NormalizedDirection(light);
        var fit = settings.AutoFit && float.IsFinite(sceneRadius) && sceneRadius > 0f;

        float near,
            far,
            size,
            distance;

        if (fit)
        {
            // Frame the sphere exactly: half-extent is its radius, and the eye sits one radius (plus
            // the near plane) back along the light so the whole sphere lies between near and far.
            near = settings.NearPlane > 0f ? settings.NearPlane : 0.1f;
            size = sceneRadius;
            distance = sceneRadius + near;
            far = distance + sceneRadius;
        }
        else
        {
            near = settings.NearPlane > 0f ? settings.NearPlane : 0.1f;
            far = settings.FarPlane > near ? settings.FarPlane : near + 1f;
            size = settings.OrthographicSize > 0f ? settings.OrthographicSize : 1f;
            distance = (near + far) * 0.5f;
        }

        var eye = sceneCenter + dir * distance;
        var view = Matrix4x4.CreateLookAt(eye, sceneCenter, UpVector(dir));
        var projection = Matrix4x4.CreateOrthographicOffCenter(-size, size, -size, size, near, far);
        return view * projection;
    }

    /// <summary>
    /// Picks the up vector the light's look-at is built from: world up for most directions, rotating
    /// smoothly towards world +Z as the light approaches vertical, where world up would be parallel
    /// to the view direction and the look-at degenerate.
    /// </summary>
    /// <remarks>
    /// The blend is the point. Switching between two fixed axes at a threshold rotates the shadow
    /// map ~90° in a single frame the moment the sun crosses it, and every shadow in the scene snaps
    /// with it. Rotating the up vector across a band spreads that same turn over the transit, so a
    /// sun passing near the zenith produces shadows that swim slightly rather than pop.
    /// </remarks>
    public static Vector3 UpVector(Vector3 direction)
    {
        const float BlendStart = 0.90f;
        const float BlendEnd = 0.999f;

        var verticality = float.IsFinite(direction.Y) ? MathF.Abs(direction.Y) : 0f;
        var t = Math.Clamp((verticality - BlendStart) / (BlendEnd - BlendStart), 0f, 1f);
        var smooth = t * t * (3f - 2f * t);

        var up = Vector3.Lerp(Vector3.UnitY, Vector3.UnitZ, smooth);
        var lengthSquared = up.LengthSquared();
        return lengthSquared > 1e-8f ? up / MathF.Sqrt(lengthSquared) : Vector3.UnitZ;
    }

    /// <summary>
    /// How strongly <paramref name="light"/> should shadow at its current elevation, in
    /// <c>[0, 1]</c>: 1 well above the horizon, ramping to 0 as it reaches the horizon and staying
    /// there below it. Pass to <see cref="Renderer3D.SetShadowMap"/>; a strength of zero means the
    /// shadow pass can be skipped entirely.
    /// </summary>
    public static float ComputeShadowStrength(DirectionalLight light, ShadowSettings settings)
    {
        var elevation = NormalizedDirection(light).Y;
        if (elevation <= 0f)
            return 0f;

        var fade = settings.HorizonFadeElevation;
        if (!float.IsFinite(fade) || fade <= 0f)
            return 1f;

        var t = Math.Clamp(elevation / fade, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Vector3 NormalizedDirection(DirectionalLight light)
    {
        var lengthSquared = light.Direction.LengthSquared();
        return float.IsFinite(lengthSquared) && lengthSquared > 0f
            ? Vector3.Normalize(light.Direction)
            : Vector3.UnitY;
    }

    /// <summary>
    /// Binds the depth framebuffer, sets the shadow-map viewport, clears depth, and uploads the
    /// light-space transform. Issue <see cref="Draw"/> calls for every shadow caster afterwards.
    /// </summary>
    public void BeginPass(DirectionalLight light, Vector3 sceneCenter) =>
        BeginPass(light, sceneCenter, sceneRadius: 0f);

    /// <summary>
    /// Begins the shadow pass, fitting the light's frustum to a bounding sphere of
    /// <paramref name="sceneRadius"/> when <see cref="ShadowSettings.AutoFit"/> is set.
    /// </summary>
    public void BeginPass(DirectionalLight light, Vector3 sceneCenter, float sceneRadius)
    {
        LightSpaceMatrix = ComputeLightSpaceMatrix(light, sceneCenter, sceneRadius, Settings);
        ShadowStrength = ComputeShadowStrength(light, Settings);
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

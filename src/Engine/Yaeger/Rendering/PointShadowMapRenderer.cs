using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders cube shadow maps for shadow-casting point lights: six depth-only face captures per
/// light into a cubemap, sampled in <c>Renderer3D.frag</c> by the fragment-to-light direction
/// against a stored linear distance. Follows <see cref="ShadowMapRenderer"/>'s conventions closely
/// — a renderer owning its own FBO/texture/bone-UBO state, a depth-only shader, and the same
/// Begin/Draw/End pass shape — extended with an outer face loop and an outer light-slot loop, since
/// a point light needs six passes instead of one and up to
/// <see cref="Renderer3D.MaxShadowCastingPointLights"/> lights can cast at once.
/// </summary>
/// <remarks>
/// Face geometry follows the same distance-not-depth technique <c>PointShadowMap.frag</c>
/// documents: each face's depth texture stores <c>|fragment - light| / range</c>, which is
/// continuous across face boundaries (unlike raw perspective depth), so sampling near an edge or
/// corner in the lighting pass never reads a discontinuous value from the "wrong" face.
/// </remarks>
public sealed class PointShadowMapRenderer : IDisposable
{
    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load(
        "PointShadowMap.vert"
    );
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "PointShadowMap.frag"
    );

    /// <summary>Maximum number of bones the shadow shader's skinning palette can hold (matches Renderer3D.MaxBones/ShadowMapRenderer.MaxBones).</summary>
    public const int MaxBones = 128;

    // Distinct from Renderer3D's (0) and ShadowMapRenderer's (1) own binding points, so no
    // renderer's bone data depends on another's draw order — same reasoning ShadowMapRenderer's own
    // BoneBlockBinding documents.
    private const uint BoneBlockBinding = 2;

    private static readonly TextureTarget[] FaceTargets =
    [
        TextureTarget.TextureCubeMapPositiveX,
        TextureTarget.TextureCubeMapNegativeX,
        TextureTarget.TextureCubeMapPositiveY,
        TextureTarget.TextureCubeMapNegativeY,
        TextureTarget.TextureCubeMapPositiveZ,
        TextureTarget.TextureCubeMapNegativeZ,
    ];

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _boneUbo;
    private readonly int _resolution;

    // One persistent FBO + cubemap depth texture per shadow-casting slot, reused across frames and
    // across whichever light currently occupies the slot — a scene where the same (or a different)
    // couple of lights cast every frame never reallocates these.
    private readonly uint[] _fbos;
    private readonly uint[] _cubemaps;

    private InstanceData[]? _instanceScratch;

    /// <summary>The settings this renderer was constructed with.</summary>
    public PointShadowSettings Settings { get; }

    /// <summary>
    /// Number of real GL draw calls issued into the current face since the most recent
    /// <see cref="BeginFace"/>. Mirrors <see cref="Renderer3D.DrawCallCount"/>/
    /// <see cref="ShadowMapRenderer.DrawCallCount"/> for this pass.
    /// </summary>
    public int DrawCallCount { get; private set; }

    public PointShadowMapRenderer(GL gl, PointShadowSettings settings)
    {
        _gl = gl;
        Settings = settings;
        _resolution = Math.Max(settings.MapResolution, 1);
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _boneUbo = CreateBoneUbo();

        _fbos = new uint[Renderer3D.MaxShadowCastingPointLights];
        _cubemaps = new uint[Renderer3D.MaxShadowCastingPointLights];
        for (var slot = 0; slot < _fbos.Length; slot++)
            (_fbos[slot], _cubemaps[slot]) = CreateCubemapFbo(_resolution);
    }

    /// <summary>The cubemap depth texture <paramref name="slot"/> renders into — sample this from <c>Renderer3D</c>.</summary>
    public uint CubemapFor(int slot) => _cubemaps[slot];

    /// <summary>
    /// Begins rendering one face of <paramref name="slot"/>'s cubemap for a light at
    /// <paramref name="lightPosition"/> with far plane <paramref name="farPlane"/> (the light's own
    /// <see cref="PointLight.Range"/>). Issue <see cref="Draw(GpuMesh, Matrix4x4)"/>/<see cref="DrawInstanced"/>
    /// calls for every caster within range, then <see cref="EndFace"/>. Call six times per light
    /// (<paramref name="faceIndex"/> in <c>[0, 6)</c>) before moving to the next slot.
    /// </summary>
    public void BeginFace(int slot, int faceIndex, Vector3 lightPosition, float farPlane)
    {
        DrawCallCount = 0;

        var resolution = (uint)_resolution;
        _gl.Viewport(0, 0, resolution, resolution);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbos[slot]);
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            FaceTargets[faceIndex],
            _cubemaps[slot],
            0
        );
        _gl.Clear((uint)ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        // Render both faces into the depth map so single-sided geometry still casts, same as
        // ShadowMapRenderer's directional pass.
        _gl.Disable(EnableCap.CullFace);

        var viewProj = ComputeFaceViewProjection(
            faceIndex,
            lightPosition,
            Settings.NearPlane,
            farPlane
        );

        _shader.Bind();
        _shader.SetUniformMatrix4("uLightSpace", viewProj);
        _shader.SetUniformVec3("uLightPos", lightPosition);
        _shader.SetUniformFloat("uFarPlane", MathF.Max(farPlane, 1e-4f));
    }

    /// <summary>Renders a single static shadow caster into the current face. Call between Begin/End.</summary>
    public void Draw(GpuMesh mesh, Matrix4x4 model)
    {
        _shader.SetUniformInt("uSkinned", 0);
        _shader.SetUniformInt("uInstanced", 0);
        _shader.SetUniformMatrix4("uModel", model);
        mesh.Draw();
        DrawCallCount++;
    }

    /// <summary>
    /// Renders a single skinned shadow caster into the current face, uploading
    /// <paramref name="bonePalette"/> to this renderer's own bone-matrix UBO. Call between
    /// Begin/End.
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
    /// Renders <paramref name="models"/> copies of <paramref name="mesh"/> into the current face in
    /// a single instanced draw call. Call between Begin/End. No-op for an empty span.
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

    /// <summary>Ends the current face. Call after the last <see cref="Draw(GpuMesh, Matrix4x4)"/>/<see cref="DrawInstanced"/> for it.</summary>
    public void EndFace()
    {
        _shader.Unbind();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>
    /// Restores the default framebuffer and the supplied viewport (the window's drawable size) once
    /// every slot/face this frame has been rendered. Call after the last <see cref="EndFace"/>.
    /// </summary>
    public void EndFrame(int viewportWidth, int viewportHeight)
    {
        _gl.Viewport(0, 0, (uint)Math.Max(viewportWidth, 1), (uint)Math.Max(viewportHeight, 1));
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.DepthFunc(DepthFunction.Less);
    }

    /// <summary>
    /// The combined view-projection for capturing cube face <paramref name="faceIndex"/> from
    /// <paramref name="lightPosition"/>: a 90° field-of-view looking down that face's direction (see
    /// <see cref="IblPrefilter.FaceDirections"/>), positioned at the light instead of the origin —
    /// <see cref="IblPrefilter.CaptureViewProjection"/>'s technique, just not fixed to the origin,
    /// since a point light's shadow is captured from wherever the light actually is.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="faceIndex"/> is outside <c>[0, 6)</c>.</exception>
    public static Matrix4x4 ComputeFaceViewProjection(
        int faceIndex,
        Vector3 lightPosition,
        float nearPlane,
        float farPlane
    )
    {
        if (faceIndex < 0 || faceIndex >= IblPrefilter.FaceDirections.Length)
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex),
                faceIndex,
                $"Must be in [0, {IblPrefilter.FaceDirections.Length})."
            );

        var (target, up) = IblPrefilter.FaceDirections[faceIndex];
        var near = nearPlane > 0f ? nearPlane : 0.05f;
        var far = farPlane > near ? farPlane : near + 1f;

        var view = Matrix4x4.CreateLookAt(lightPosition, lightPosition + target, up);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 1f, near, far);
        return view * projection;
    }

    /// <summary>
    /// Selects up to <see cref="Renderer3D.MaxShadowCastingPointLights"/> indices, into
    /// <paramref name="lights"/>, of lights with <see cref="PointLight.CastsShadows"/> set —
    /// nearest to <paramref name="cameraPosition"/> first, since a capped shadow budget is most
    /// worth spending on what's closest to the viewer. Lights past the cap still light the scene
    /// (see <see cref="PointLight.CastsShadows"/>'s remarks); they're simply not selected here.
    /// </summary>
    /// <param name="destination">
    /// Filled with selected indices, most-worth-shadowing first. Must be at least
    /// <see cref="Renderer3D.MaxShadowCastingPointLights"/> long.
    /// </param>
    /// <returns>How many indices were written to <paramref name="destination"/> — between 0 and its length.</returns>
    public static int SelectShadowCasters(
        ReadOnlySpan<(Vector3 Position, PointLight Light)> lights,
        Vector3 cameraPosition,
        Span<int> destination
    )
    {
        if (destination.IsEmpty)
            return 0;

        // The current best `destination.Length` candidates found so far, kept sorted ascending by
        // distance in lockstep with `destination` — small enough (a handful of slots) that
        // insertion into this short prefix is simpler and cheaper than sorting every candidate.
        Span<float> bestDistances = stackalloc float[destination.Length];
        var count = 0;

        for (var i = 0; i < lights.Length; i++)
        {
            if (!lights[i].Light.CastsShadows)
                continue;

            var distance = Vector3.DistanceSquared(lights[i].Position, cameraPosition);

            if (count < destination.Length)
            {
                var insertAt = count;
                while (insertAt > 0 && bestDistances[insertAt - 1] > distance)
                {
                    bestDistances[insertAt] = bestDistances[insertAt - 1];
                    destination[insertAt] = destination[insertAt - 1];
                    insertAt--;
                }
                bestDistances[insertAt] = distance;
                destination[insertAt] = i;
                count++;
            }
            else if (distance < bestDistances[count - 1])
            {
                var insertAt = count - 1;
                while (insertAt > 0 && bestDistances[insertAt - 1] > distance)
                {
                    bestDistances[insertAt] = bestDistances[insertAt - 1];
                    destination[insertAt] = destination[insertAt - 1];
                    insertAt--;
                }
                bestDistances[insertAt] = distance;
                destination[insertAt] = i;
            }
        }

        return count;
    }

    private unsafe (uint Fbo, uint Cubemap) CreateCubemapFbo(int resolution)
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        var cubemap = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, cubemap);
        foreach (var face in FaceTargets)
        {
            _gl.TexImage2D(
                face,
                0,
                (int)InternalFormat.DepthComponent24,
                (uint)resolution,
                (uint)resolution,
                0,
                PixelFormat.DepthComponent,
                PixelType.UnsignedInt,
                null
            );
        }
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapR,
            (int)GLEnum.ClampToEdge
        );
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);

        var fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        // Attach face 0 just to let the completeness check below pass at construction time;
        // BeginFace re-attaches the actual face being rendered every call.
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            FaceTargets[0],
            cubemap,
            0
        );
        _gl.DrawBuffer(DrawBufferMode.None);
        _gl.ReadBuffer(ReadBufferMode.None);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException(
                $"Point shadow map framebuffer incomplete: {status}"
            );

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return (fbo, cubemap);
    }

    // Allocates the bone-matrix uniform buffer (MaxBones mat4s) and links it to the shadow shader's
    // "Bones" block via this renderer's own binding point. Mirrors Renderer3D.CreateBoneUbo/
    // ShadowMapRenderer.CreateBoneUbo.
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
        _gl.DeleteBuffer(_boneUbo);
        foreach (var fbo in _fbos)
            _gl.DeleteFramebuffer(fbo);
        foreach (var cubemap in _cubemaps)
            _gl.DeleteTexture(cubemap);
    }
}

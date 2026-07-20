using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Prefilters a skybox <see cref="CubemapTexture"/> into the GPU resources image-based lighting
/// needs: a diffuse irradiance cubemap, a roughness-to-mip prefiltered specular cubemap, and a
/// split-sum BRDF LUT (computed once, lazily, and shared across every prefiltered environment —
/// it depends only on the BRDF, not the source cubemap). Each is rendered offscreen by drawing a
/// unit cube (vertex positions double as cubemap sample directions, matching
/// <see cref="SkyboxRenderer"/>'s technique) into a cubemap-face framebuffer attachment from six
/// capture directions, following <see cref="ShadowMapRenderer"/>'s pattern of a renderer owning
/// its own FBO state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Colour space:</b> skybox cubemaps are loaded as raw (untagged) 8-bit textures — see
/// <see cref="CubemapTexture"/> — so <see cref="SkyboxRenderer"/> can display them directly with
/// no conversion. Lighting integrals, however, need linear values, so every convolution shader
/// here linearises a source sample (<c>pow(rgb, 2.2)</c>) before weighting it. The resulting
/// irradiance/prefiltered textures are written back already linear, so
/// <c>Renderer3D</c>'s PBR path samples them directly with no further linearisation.
/// </para>
/// <para>
/// <b>Cost:</b> prefiltering is a one-off, GPU-bound pass per call to <see cref="Prefilter"/> —
/// call it once when a skybox is registered (see <see cref="EnvironmentMapRegistry"/>), not every
/// frame; re-prefiltering a runtime-changed skybox is out of scope.
/// </para>
/// </remarks>
public sealed class IblPrefilter : IDisposable
{
    /// <summary>Resolution (per face) of the diffuse irradiance cubemap.</summary>
    public const int IrradianceResolution = 32;

    /// <summary>Resolution (per face) of the prefiltered specular cubemap's sharpest (mip 0) level.</summary>
    public const int PrefilteredBaseResolution = 128;

    /// <summary>Number of mip levels in the prefiltered specular cubemap (roughness 0 → 1).</summary>
    public const int PrefilteredMipLevels = 5;

    /// <summary>Resolution (both axes) of the split-sum BRDF LUT.</summary>
    public const int BrdfLutResolution = 128;

    /// <summary>
    /// Target/up direction for each cubemap face capture, in Right/Left/Top/Bottom/Front/Back
    /// order — matching <see cref="CubemapTexture"/>'s face order and OpenGL's
    /// <c>TEXTURE_CUBE_MAP_POSITIVE_X..NEGATIVE_Z</c> sequence.
    /// </summary>
    public static readonly (Vector3 Target, Vector3 Up)[] FaceDirections =
    [
        (new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f)), // +X right
        (new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f)), // -X left
        (new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f)), // +Y top
        (new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f)), // -Y bottom
        (new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f)), // +Z front
        (new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f)), // -Z back
    ];

    private static readonly TextureTarget[] FaceTargets =
    [
        TextureTarget.TextureCubeMapPositiveX,
        TextureTarget.TextureCubeMapNegativeX,
        TextureTarget.TextureCubeMapPositiveY,
        TextureTarget.TextureCubeMapNegativeY,
        TextureTarget.TextureCubeMapPositiveZ,
        TextureTarget.TextureCubeMapNegativeZ,
    ];

    /// <summary>
    /// The combined view-projection matrix for capturing cubemap face <paramref name="faceIndex"/>
    /// from the origin: a 90° field-of-view looking down that face's direction (see
    /// <see cref="FaceDirections"/>), matching the target cubemap face's own projection exactly so
    /// the rendered content maps onto it with no seams.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="faceIndex"/> is outside <c>[0, 6)</c>.
    /// </exception>
    public static Matrix4x4 CaptureViewProjection(int faceIndex, float near = 0.1f, float far = 10f)
    {
        if (faceIndex < 0 || faceIndex >= FaceDirections.Length)
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex),
                faceIndex,
                $"Must be in [0, {FaceDirections.Length})."
            );

        var (target, up) = FaceDirections[faceIndex];
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, target, up);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 1f, near, far);
        return view * projection;
    }

    /// <summary>
    /// Maps mip level <paramref name="mip"/> (of <paramref name="mipLevelCount"/> total) to the
    /// roughness value the specular prefilter convolution should use for that level: mip 0 is
    /// perfectly sharp (roughness 0), the last mip is fully rough (roughness 1), and intermediate
    /// levels interpolate linearly. A single-level chain always maps to roughness 0.
    /// </summary>
    public static float RoughnessForMip(int mip, int mipLevelCount)
    {
        if (mipLevelCount <= 1)
            return 0f;

        var clampedMip = Math.Clamp(mip, 0, mipLevelCount - 1);
        return (float)clampedMip / (mipLevelCount - 1);
    }

    /// <summary>
    /// Halves <paramref name="baseResolution"/> <paramref name="mip"/> times, clamped to at least
    /// one texel — the standard mip-chain resolution progression.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mip"/> is negative.</exception>
    public static int MipResolution(int baseResolution, int mip)
    {
        if (mip < 0)
            throw new ArgumentOutOfRangeException(nameof(mip), mip, "Must be non-negative.");

        return Math.Max(baseResolution >> mip, 1);
    }

    // Vertex shader shared by every capture-cube pass: draws the unit cube from the origin using a
    // capture view-projection, and hands the (unnormalised) cube position to the fragment shader
    // as both the sample direction and the surface normal (the cube is a unit sphere-of-directions
    // proxy, not real geometry).
    private static readonly string CubeVertexShaderSource = EmbeddedShaderSource.Load(
        "IblCube.vert"
    );

    // Diffuse irradiance convolution: for each output direction N, integrates incoming radiance
    // over the cosine-weighted hemisphere. Low frequency by nature, so a modest angular step keeps
    // this fast even at IrradianceResolution.
    private static readonly string IrradianceFragmentShaderSource = EmbeddedShaderSource.Load(
        "IblIrradiance.frag"
    );

    // Shared low-discrepancy sampling helpers (Hammersley sequence + GGX importance sampling),
    // injected verbatim into both the specular prefilter and BRDF LUT shaders below since GLSL
    // has no cross-shader include mechanism at this GL version.
    private static readonly string ImportanceSamplingGlsl = EmbeddedShaderSource.Load(
        "IblImportanceSampling.glsl"
    );

    // Specular prefilter: for a given roughness (one draw per mip level), importance-samples GGX
    // reflection vectors around N (approximated as N == V == R, the standard split-sum
    // assumption) and accumulates NdotL-weighted incoming radiance.
    private static readonly string PrefilterFragmentShaderSource = EmbeddedShaderSource.Load(
        "IblPrefilter.frag"
    );

    private static readonly string QuadVertexShaderSource = EmbeddedShaderSource.Load(
        "IblQuad.vert"
    );

    // Split-sum BRDF LUT: integrates the specular BRDF's scale/bias (Karis, "Real Shading in
    // Unreal Engine 4") over (NdotV, roughness), independent of any environment — this is why one
    // LUT can be shared across every prefiltered skybox.
    private static readonly string BrdfLutFragmentShaderSource = EmbeddedShaderSource.Load(
        "IblBrdfLut.frag"
    );

    private static readonly float[] QuadVertices =
    [
        // pos.xy,   uv.xy
        -1f,
        1f,
        0f,
        1f,
        -1f,
        -1f,
        0f,
        0f,
        1f,
        -1f,
        1f,
        0f,
        -1f,
        1f,
        0f,
        1f,
        1f,
        -1f,
        1f,
        0f,
        1f,
        1f,
        1f,
        1f,
    ];

    private readonly GL _gl;
    private readonly Shader _irradianceShader;
    private readonly Shader _prefilterShader;
    private readonly Shader _brdfLutShader;
    private readonly uint _fbo;
    private readonly uint _cubeVao;
    private readonly uint _cubeVbo;
    private readonly uint _quadVao;
    private readonly uint _quadVbo;

    // Computed lazily on the first Prefilter() call and shared by every returned EnvironmentMap.
    private uint? _brdfLut;

    public unsafe IblPrefilter(GL gl)
    {
        _gl = gl;

        var importanceSampling = ImportanceSamplingGlsl.Trim();
        _irradianceShader = new Shader(gl, CubeVertexShaderSource, IrradianceFragmentShaderSource);
        _prefilterShader = new Shader(
            gl,
            CubeVertexShaderSource,
            PrefilterFragmentShaderSource.Replace("IMPORTANCE_SAMPLING_GLSL", importanceSampling)
        );
        _brdfLutShader = new Shader(
            gl,
            QuadVertexShaderSource,
            BrdfLutFragmentShaderSource.Replace("IMPORTANCE_SAMPLING_GLSL", importanceSampling)
        );

        _fbo = _gl.GenFramebuffer();

        (_cubeVao, _cubeVbo) = CreateCubeMesh();
        (_quadVao, _quadVbo) = CreateQuadMesh();
    }

    /// <summary>
    /// Prefilters <paramref name="source"/> into a new <see cref="EnvironmentMap"/>: a diffuse
    /// irradiance cubemap, a mip-chained prefiltered specular cubemap, and (on the first call) the
    /// shared BRDF LUT. Runs synchronously via offscreen GPU passes — expect this to take
    /// noticeably longer than a single frame; call it during scene setup, not every frame.
    /// </summary>
    /// <param name="source">The skybox cubemap to prefilter.</param>
    /// <param name="viewportWidth">
    /// Current window viewport width, restored afterward — the offscreen passes resize the
    /// viewport to each capture resolution as they run.
    /// </param>
    /// <param name="viewportHeight">Current window viewport height, restored the same way.</param>
    public EnvironmentMap Prefilter(CubemapTexture source, int viewportWidth, int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(source);

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        try
        {
            _brdfLut ??= RenderBrdfLut();

            var irradianceMap = RenderIrradianceCubemap(source);
            var (prefilteredMap, mipCount) = RenderPrefilteredCubemap(source);

            return new EnvironmentMap(_gl, irradianceMap, prefilteredMap, mipCount, _brdfLut.Value);
        }
        finally
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)Math.Max(viewportWidth, 1), (uint)Math.Max(viewportHeight, 1));
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(TriangleFace.Back);
        }
    }

    private uint RenderIrradianceCubemap(CubemapTexture source)
    {
        var cubemap = CreateEmptyCubemap(IrradianceResolution, mipLevels: 1);

        _gl.Viewport(0, 0, IrradianceResolution, IrradianceResolution);
        _irradianceShader.Bind();
        _irradianceShader.SetUniformInt("uSource", 0);
        source.Bind(TextureUnit.Texture0);

        for (var face = 0; face < FaceDirections.Length; face++)
        {
            _irradianceShader.SetUniformMatrix4("uViewProj", CaptureViewProjection(face));
            AttachCubeFace(cubemap, face, mip: 0);
            _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
            DrawUnitCube();
        }

        source.Unbind(TextureUnit.Texture0);
        _irradianceShader.Unbind();
        return cubemap;
    }

    private (uint Handle, int MipCount) RenderPrefilteredCubemap(CubemapTexture source)
    {
        var cubemap = CreateEmptyCubemap(PrefilteredBaseResolution, PrefilteredMipLevels);

        _prefilterShader.Bind();
        _prefilterShader.SetUniformInt("uSource", 0);
        source.Bind(TextureUnit.Texture0);

        for (var mip = 0; mip < PrefilteredMipLevels; mip++)
        {
            var mipResolution = MipResolution(PrefilteredBaseResolution, mip);
            _gl.Viewport(0, 0, (uint)mipResolution, (uint)mipResolution);
            _prefilterShader.SetUniformFloat(
                "uRoughness",
                RoughnessForMip(mip, PrefilteredMipLevels)
            );

            for (var face = 0; face < FaceDirections.Length; face++)
            {
                _prefilterShader.SetUniformMatrix4("uViewProj", CaptureViewProjection(face));
                AttachCubeFace(cubemap, face, mip);
                _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
                DrawUnitCube();
            }
        }

        source.Unbind(TextureUnit.Texture0);
        _prefilterShader.Unbind();
        return (cubemap, PrefilteredMipLevels);
    }

    private unsafe uint RenderBrdfLut()
    {
        var lut = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, lut);
        _gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            (int)InternalFormat.Rgba,
            BrdfLutResolution,
            BrdfLutResolution,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            null
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );

        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            lut,
            0
        );
        _gl.Viewport(0, 0, BrdfLutResolution, BrdfLutResolution);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        _brdfLutShader.Bind();
        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
        _brdfLutShader.Unbind();

        return lut;
    }

    private void AttachCubeFace(uint cubemap, int face, int mip) =>
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            FaceTargets[face],
            cubemap,
            mip
        );

    private void DrawUnitCube()
    {
        _gl.BindVertexArray(_cubeVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
        _gl.BindVertexArray(0);
    }

    private unsafe uint CreateEmptyCubemap(int baseResolution, int mipLevels)
    {
        var handle = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, handle);

        for (var mip = 0; mip < mipLevels; mip++)
        {
            var mipResolution = MipResolution(baseResolution, mip);
            foreach (var face in FaceTargets)
            {
                _gl.TexImage2D(
                    face,
                    mip,
                    (int)InternalFormat.Rgba,
                    (uint)mipResolution,
                    (uint)mipResolution,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    null
                );
            }
        }

        var minFilter = mipLevels > 1 ? GLEnum.LinearMipmapLinear : GLEnum.Linear;
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)minFilter
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
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
        // Every level was allocated with real storage above (never generated via
        // GenerateMipmap, which would overwrite the roughness-prefiltered content with a naive
        // box-filtered downsample), so tell the driver the valid range explicitly.
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMaxLevel,
            mipLevels - 1
        );

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        return handle;
    }

    private unsafe (uint Vao, uint Vbo) CreateCubeMesh()
    {
        var vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        var vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* ptr = UnitCubeGeometry.Vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(UnitCubeGeometry.Vertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw
            );
        }

        _gl.VertexAttribPointer(
            0,
            3,
            VertexAttribPointerType.Float,
            false,
            3 * sizeof(float),
            (void*)0
        );
        _gl.EnableVertexAttribArray(0);

        _gl.BindVertexArray(0);
        return (vao, vbo);
    }

    private unsafe (uint Vao, uint Vbo) CreateQuadMesh()
    {
        var vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        var vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* ptr = QuadVertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(QuadVertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw
            );
        }

        _gl.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            4 * sizeof(float),
            (void*)0
        );
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            4 * sizeof(float),
            (void*)(2 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
        return (vao, vbo);
    }

    public void Dispose()
    {
        _irradianceShader.Dispose();
        _prefilterShader.Dispose();
        _brdfLutShader.Dispose();
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteVertexArray(_cubeVao);
        _gl.DeleteBuffer(_cubeVbo);
        _gl.DeleteVertexArray(_quadVao);
        _gl.DeleteBuffer(_quadVbo);
        if (_brdfLut is { } lut)
            _gl.DeleteTexture(lut);
    }
}

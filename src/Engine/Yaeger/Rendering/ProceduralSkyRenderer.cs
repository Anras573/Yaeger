using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders a shader-computed sky — gradient, sun and moon discs, a rotating star field, and
/// drifting clouds — with no cubemap assets. Same draw pattern as <see cref="SkyboxRenderer"/>
/// (a unit cube viewed from inside with a rotation-only view matrix, drawn with <c>LEQUAL</c> depth
/// inside the <see cref="Renderer3D.BeginFrame3D"/>/<see cref="Renderer3D.EndFrame3D"/> window) —
/// the two are independent, dispatched by <c>MeshRenderSystem</c> based on which of
/// <see cref="Skybox"/>/<see cref="ProceduralSky"/> the scene carries.
/// </summary>
public sealed class ProceduralSkyRenderer : IDisposable
{
    /// <summary>
    /// Per-face resolution of the cubemap <see cref="Bake"/> renders into. Modest on purpose —
    /// <see cref="IblPrefilter"/> immediately reduces whatever is baked here down to a 32³
    /// irradiance map and a 128³ specular chain, so a sharp source cubemap buys nothing.
    /// </summary>
    public const int BakeResolution = 128;

    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load("Skybox.vert");
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "ProceduralSky.frag"
    );

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
    private readonly uint _vao;
    private readonly uint _vbo;

    // Lazily allocated on the first Bake() call and reused by every subsequent call — a re-bake
    // must not grow GL handles over a long-running day/night cycle (see ProceduralSkyIbl).
    private uint? _bakeFbo;
    private uint? _bakeCubemap;

    public unsafe ProceduralSkyRenderer(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
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
    }

    /// <summary>
    /// Draws the sky using a rotation-only view matrix derived from <paramref name="view"/>. Must be
    /// called while depth testing is active (inside a <see cref="Renderer3D.BeginFrame3D"/> /
    /// <see cref="Renderer3D.EndFrame3D"/> block).
    /// </summary>
    public void Draw(ProceduralSky sky, Matrix4x4 view, Matrix4x4 projection)
    {
        // Strip translation so the sky stays centred on the camera, same as SkyboxRenderer.
        var rotationOnly = view;
        rotationOnly.M41 = 0f;
        rotationOnly.M42 = 0f;
        rotationOnly.M43 = 0f;

        var starRotation = ProceduralSkyMath.StarRotation(sky.SunDirection);
        var cloudOffset = ProceduralSkyMath.CloudScrollOffset(sky.CloudWind, sky.Elapsed);

        // LEQUAL so the sky (depth = 1.0 after pos.xyww) renders behind all geometry.
        _gl.DepthFunc(DepthFunction.Lequal);
        // The cube is viewed from inside, so back-face culling would discard every face.
        _gl.Disable(EnableCap.CullFace);
        try
        {
            _shader.Bind();
            _shader.SetUniformMatrix4("uView", rotationOnly);
            _shader.SetUniformMatrix4("uProjection", projection);
            SetSkyUniforms(sky, starRotation, cloudOffset);
            DrawUnitCube();
            _shader.Unbind();
        }
        finally
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.DepthFunc(DepthFunction.Less);
        }
    }

    /// <summary>
    /// Renders <paramref name="sky"/> into a persistent, internally-owned cubemap — six face
    /// captures through an offscreen FBO, following <see cref="IblPrefilter"/>'s own capture pattern
    /// (a unit cube whose vertex positions double as sample directions, drawn from six 90°
    /// view-projections into a cubemap-face attachment) and <see cref="ShadowMapRenderer"/>'s
    /// convention of a renderer owning its own GL state. The returned handle is reused across calls
    /// — a caller re-baking every few seconds over a long-running day/night cycle does not leak
    /// texture handles — so treat it as valid only until the next <see cref="Bake"/> call, the same
    /// lifetime contract <see cref="IblPrefilter.Prefilter(uint, int, int)"/> expects of its source.
    /// </summary>
    /// <param name="sky">The sky to bake.</param>
    /// <param name="viewportWidth">Current window viewport width, restored afterward — the bake
    /// passes resize the viewport to <see cref="BakeResolution"/> while they run.</param>
    /// <param name="viewportHeight">Current window viewport height, restored the same way.</param>
    /// <returns>The GL cubemap texture handle baked into.</returns>
    public unsafe uint Bake(ProceduralSky sky, int viewportWidth, int viewportHeight)
    {
        EnsureBakeResources();

        var starRotation = ProceduralSkyMath.StarRotation(sky.SunDirection);
        var cloudOffset = ProceduralSkyMath.CloudScrollOffset(sky.CloudWind, sky.Elapsed);

        // No depth attachment on this FBO and the fragment shader is direction-only (see
        // ProceduralSky.frag), so depth testing/culling would only get in the way — same reasoning
        // IblPrefilter.Prefilter disables both around its own capture passes.
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _bakeFbo!.Value);
        _gl.Viewport(0, 0, BakeResolution, BakeResolution);
        try
        {
            _shader.Bind();
            SetSkyUniforms(sky, starRotation, cloudOffset);

            for (var face = 0; face < IblPrefilter.FaceDirections.Length; face++)
            {
                var (target, up) = IblPrefilter.FaceDirections[face];
                var view = Matrix4x4.CreateLookAt(Vector3.Zero, target, up);
                var projection = Matrix4x4.CreatePerspectiveFieldOfView(
                    MathF.PI / 2f,
                    1f,
                    0.1f,
                    10f
                );
                _shader.SetUniformMatrix4("uView", view);
                _shader.SetUniformMatrix4("uProjection", projection);

                _gl.FramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    FaceTargets[face],
                    _bakeCubemap!.Value,
                    0
                );
                _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
                DrawUnitCube();
            }

            _shader.Unbind();
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

        return _bakeCubemap.Value;
    }

    // Every uniform Draw/Bake share — everything except uView/uProjection, which differ between a
    // camera's view and a per-face capture view.
    private void SetSkyUniforms(ProceduralSky sky, Matrix4x4 starRotation, Vector2 cloudOffset)
    {
        _shader.SetUniformVec3("uSunDirection", sky.SunDirection);
        _shader.SetUniformVec3("uMoonDirection", sky.MoonDirection);
        _shader.SetUniformFloat("uDaylightFactor", sky.DaylightFactor);
        _shader.SetUniformFloat("uStarDensity", sky.StarDensity);
        _shader.SetUniformFloat("uMoonPhase", sky.MoonPhase);
        _shader.SetUniformFloat("uCloudScale", sky.CloudScale);
        _shader.SetUniformFloat("uCloudCoverage", sky.CloudCoverage);
        _shader.SetUniformVec2("uCloudOffset", cloudOffset);
        _shader.SetUniformMatrix3("uStarRotation", starRotation);
    }

    private void DrawUnitCube()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
        _gl.BindVertexArray(0);
    }

    [MemberNotNull(nameof(_bakeFbo))]
    [MemberNotNull(nameof(_bakeCubemap))]
    private unsafe void EnsureBakeResources()
    {
        if (_bakeFbo != null && _bakeCubemap != null)
            return;

        var cubemap = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, cubemap);
        foreach (var face in FaceTargets)
        {
            _gl.TexImage2D(
                face,
                0,
                (int)InternalFormat.Rgba,
                BakeResolution,
                BakeResolution,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                null
            );
        }
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
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
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);

        _bakeCubemap = cubemap;
        _bakeFbo = _gl.GenFramebuffer();
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        if (_bakeFbo is { } fbo)
            _gl.DeleteFramebuffer(fbo);
        if (_bakeCubemap is { } cubemap)
            _gl.DeleteTexture(cubemap);
    }
}

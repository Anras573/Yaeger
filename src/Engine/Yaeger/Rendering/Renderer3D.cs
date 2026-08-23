using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders 3D meshes with MVP transforms, depth testing, and back-face culling.
/// Independent of the 2D <see cref="Renderer"/> pipeline.
/// </summary>
/// <remarks>
/// Split by concern across partial files: this one holds construction, per-frame lifecycle,
/// and shared helpers. See <c>Renderer3D.Lighting.cs</c> (directional/point/spot lights),
/// <c>Renderer3D.Shadows.cs</c> (directional + point shadow maps),
/// <c>Renderer3D.Ibl.cs</c> (image-based lighting), <c>Renderer3D.Fog.cs</c> (distance fog),
/// <c>Renderer3D.Draw.cs</c> (mesh draw/instancing/materials), <c>Renderer3D.Skinning.cs</c>
/// (GPU skinning, immediate and instanced), <c>Renderer3D.Particles.cs</c> (billboard
/// particles + soft-particle scene depth), and <c>Renderer3D.Textures.cs</c> (the 1x1
/// fallback textures every other part binds as its "off" state).
/// </remarks>
public sealed partial class Renderer3D : IDisposable
{
    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load(
        "Renderer3D.vert"
    );
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "Renderer3D.frag"
    );

    /// <summary>
    /// Number of real GL draw calls (<c>glDrawElements</c>/<c>glDrawElementsInstanced</c>) issued
    /// since the last <see cref="BeginFrame3D"/>. An instanced group of any size counts as one call,
    /// so this is the metric that demonstrates instancing turning O(N) draw calls into O(1).
    /// </summary>
    public int DrawCallCount { get; private set; }

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _defaultTexture;
    private readonly uint _defaultNormalTexture;
    private readonly uint _defaultCubemap;

    /// <param name="gl">The window's OpenGL context.</param>
    /// <param name="hdrOutput">
    /// When false (the default), the PBR path's final colour is Reinhard tone-mapped and
    /// gamma-encoded to sRGB in-shader before being written out — the original behaviour, correct
    /// when rendering straight to the backbuffer or an LDR <see cref="RenderTarget"/>. When true,
    /// that in-shader compression is skipped and the PBR path instead writes linear HDR colour
    /// (values above 1.0 preserved) — pair with a <see cref="PostProcessStack"/> constructed with
    /// <c>hdr: true</c> and a <see cref="ToneMapEffect"/> as the chain's last effect, which then
    /// perform the tone-mapping/gamma-encoding once, on the whole post-processed frame, instead of
    /// per-fragment here. Has no effect on the Blinn-Phong path, which was never gamma-encoded
    /// in-shader either way — see docs/pbr.md's colour-space notes.
    /// </param>
    public Renderer3D(GL gl, bool hdrOutput = false)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _defaultTexture = CreateWhiteTexture();
        _defaultNormalTexture = CreateFlatNormalTexture();
        _defaultCubemap = CreateWhiteCubemap();
        _boneUbo = CreateBoneUbo();
        (_bonePaletteBuffer, _bonePaletteTexture) = CreateBonePaletteTextureBuffer();
        _particleShader = new Shader(gl, ParticleVertexShaderSource, ParticleFragmentShaderSource);
        (_particleVao, _particleQuadVbo) = CreateParticleQuad();
        _particleShader.Bind();
        _particleShader.SetUniformInt("uTexture", 0);
        _particleShader.SetUniformInt("uSceneDepth", 9);
        _particleShader.Unbind();
        BindSamplerUnits();
        BindDefaultPbrTextures();
        // DisableShadows also binds the default texture on unit 5, so no separate setup is needed.
        DisableShadows();
        // DisableIBL also binds the default cubemap/texture on units 6-8.
        DisableIBL();
        // Establishes the "off" uniform state so a scene that never calls SetFog is unchanged.
        DisableFog();
        // Establishes the "off" (hard-cutoff) uniform state so a scene that never calls
        // SetSceneDepth renders particles exactly as it did before soft particles existed.
        DisableSceneDepth();
        // Skinning and instancing are opt-in per draw; default to the static-mesh, non-instanced path.
        _shader.Bind();
        _shader.SetUniformInt("uSkinned", 0);
        _shader.SetUniformInt("uInstanced", 0);
        _shader.SetUniformInt("uHdrOutput", hdrOutput ? 1 : 0);
        _shader.Unbind();
        SetSceneLighting(DirectionalLight.Default, Vector3.Zero);
        // Matches the constant the fragment shader used to hardcode, so a scene that never calls
        // SetAmbient is unchanged.
        SetAmbient(AmbientLight.Default);
        // Start with no point/spot lights so scenes that never call SetPointLights/SetSpotLights
        // (the pre-existing single-directional-light path) render exactly as before.
        SetPointLights([]);
        SetSpotLights([]);
        // DisablePointShadows also binds the default cubemap on units 10/11, so no separate setup
        // is needed — mirrors DisableShadows/DisableIBL above.
        DisablePointShadows();
    }

    // Sampler-to-texture-unit assignments never change after link, so set them once here rather
    // than re-uploading them on every Draw call.
    private void BindSamplerUnits()
    {
        _shader.Bind();
        _shader.SetUniformInt("uDiffuse", 0);
        _shader.SetUniformInt("uNormalMap", 1);
        _shader.SetUniformInt("uMetallicRoughnessMap", 2);
        _shader.SetUniformInt("uAoMap", 3);
        _shader.SetUniformInt("uEmissiveMap", 4);
        _shader.SetUniformInt("uShadowMap", 5);
        _shader.SetUniformInt("uIrradianceMap", 6);
        _shader.SetUniformInt("uPrefilteredMap", 7);
        _shader.SetUniformInt("uBrdfLut", 8);
        for (var slot = 0; slot < MaxShadowCastingPointLights; slot++)
            _shader.SetUniformInt(PointShadowMapSamplerNames[slot], 10 + slot);
        _shader.SetUniformInt("uBonePalette", BonePaletteTextureUnit);
        _shader.Unbind();
    }

    /// <summary>
    /// Enables depth testing and back-face culling, and clears the colour and depth buffers.
    /// Call once at the start of the 3D pass each frame.
    /// </summary>
    public void BeginFrame3D()
    {
        DrawCallCount = 0;
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
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

    // Builds struct-array element uniform names ("uName[i].field") once so the per-frame upload
    // loops in Renderer3D.Lighting.cs/Renderer3D.Shadows.cs never allocate a fresh string per field.
    private static string[] BuildNames(string array, string field, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{array}[{i}].{field}";
        return names;
    }

    // Like BuildNames, but for plain (non-struct-field) uniform arrays — "uName0", "uName1", not
    // struct-array element names — used for the point shadow slots, whose samplers can't be a real
    // GLSL sampler array (dynamic indexing of a sampler array isn't defined in GLSL 330; see
    // Renderer3D.Shadows.cs's SetPointShadows).
    private static string[] BuildFlatNames(string name, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{name}{i}";
        return names;
    }

    // Guards against NaN/negative values leaking into shader uniforms (used by SetSceneLighting,
    // SetPointLights, SetSpotLights, SetShadowMap, SetPointShadows, and SetFog).
    private static float SanitizeNonNegative(float value) =>
        float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteTexture(_defaultTexture);
        _gl.DeleteTexture(_defaultNormalTexture);
        _gl.DeleteTexture(_defaultCubemap);
        _gl.DeleteBuffer(_boneUbo);
        _gl.DeleteTexture(_bonePaletteTexture);
        _gl.DeleteBuffer(_bonePaletteBuffer);

        _particleShader.Dispose();
        _gl.DeleteVertexArray(_particleVao);
        _gl.DeleteBuffer(_particleQuadVbo);
        if (_hasParticleInstanceBuffer)
            _gl.DeleteBuffer(_particleInstanceVbo);
    }
}

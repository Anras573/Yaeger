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
    private static readonly string VertexShaderSource = EmbeddedShaderSource.Load(
        "Renderer3D.vert"
    );
    private static readonly string FragmentShaderSource = EmbeddedShaderSource.Load(
        "Renderer3D.frag"
    );
    private static readonly string ParticleVertexShaderSource = EmbeddedShaderSource.Load(
        "ParticleBillboard.vert"
    );
    private static readonly string ParticleFragmentShaderSource = EmbeddedShaderSource.Load(
        "ParticleBillboard.frag"
    );

    // Unit quad shared by every particle draw: corner (xy, [-0.5, 0.5]) + texcoord (zw, [0, 1]) per
    // vertex, two triangles, no index buffer needed for six vertices.
    private static readonly float[] ParticleQuadVertices =
    [
        -0.5f,
        -0.5f,
        0f,
        0f,
        0.5f,
        -0.5f,
        1f,
        0f,
        0.5f,
        0.5f,
        1f,
        1f,
        -0.5f,
        -0.5f,
        0f,
        0f,
        0.5f,
        0.5f,
        1f,
        1f,
        -0.5f,
        0.5f,
        0f,
        1f,
    ];

    /// <summary>Maximum number of bones the vertex shader's skinning palette can hold (matches MAX_BONES in GLSL).</summary>
    public const int MaxBones = 128;

    // Binding point linking the "Bones" uniform block to the bone-matrix UBO. Arbitrary but must not
    // collide with any other uniform block binding (the renderer has none).
    private const uint BoneBlockBinding = 0;

    /// <summary>
    /// Number of real GL draw calls (<c>glDrawElements</c>/<c>glDrawElementsInstanced</c>) issued
    /// since the last <see cref="BeginFrame3D"/>. An instanced group of any size counts as one call,
    /// so this is the metric that demonstrates instancing turning O(N) draw calls into O(1).
    /// </summary>
    public int DrawCallCount { get; private set; }

    /// <summary>Maximum number of point lights the fragment shader can accumulate per frame.</summary>
    public const int MaxPointLights = 16;

    /// <summary>Maximum number of spot lights the fragment shader can accumulate per frame.</summary>
    public const int MaxSpotLights = 8;

    // Per-light uniform names depend only on the array index, so build them once and reuse them
    // every frame. Interpolating them inside the per-frame upload loops would allocate a fresh
    // string per light field on every call.
    private static readonly string[] PointPositionNames;
    private static readonly string[] PointColorNames;
    private static readonly string[] PointIntensityNames;
    private static readonly string[] PointRangeNames;
    private static readonly string[] SpotPositionNames;
    private static readonly string[] SpotDirectionNames;
    private static readonly string[] SpotColorNames;
    private static readonly string[] SpotIntensityNames;
    private static readonly string[] SpotInnerCosNames;
    private static readonly string[] SpotOuterCosNames;
    private static readonly string[] SpotRangeNames;

    static Renderer3D()
    {
        PointPositionNames = BuildNames("uPointLights", "position", MaxPointLights);
        PointColorNames = BuildNames("uPointLights", "color", MaxPointLights);
        PointIntensityNames = BuildNames("uPointLights", "intensity", MaxPointLights);
        PointRangeNames = BuildNames("uPointLights", "range", MaxPointLights);
        SpotPositionNames = BuildNames("uSpotLights", "position", MaxSpotLights);
        SpotDirectionNames = BuildNames("uSpotLights", "direction", MaxSpotLights);
        SpotColorNames = BuildNames("uSpotLights", "color", MaxSpotLights);
        SpotIntensityNames = BuildNames("uSpotLights", "intensity", MaxSpotLights);
        SpotInnerCosNames = BuildNames("uSpotLights", "innerCos", MaxSpotLights);
        SpotOuterCosNames = BuildNames("uSpotLights", "outerCos", MaxSpotLights);
        SpotRangeNames = BuildNames("uSpotLights", "range", MaxSpotLights);
    }

    private static string[] BuildNames(string array, string field, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{array}[{i}].{field}";
        return names;
    }

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _defaultTexture;
    private readonly uint _defaultNormalTexture;
    private readonly uint _defaultCubemap;
    private readonly uint _boneUbo;

    // Particle billboards: a separate unlit shader/VAO from the lit Blinn-Phong/PBR path above —
    // same reasoning as SkyboxRenderer/UiRenderer staying outside Renderer3D's main shader.
    private readonly Shader _particleShader;
    private readonly uint _particleVao;
    private readonly uint _particleQuadVbo;
    private uint _particleInstanceVbo;
    private int _particleInstanceCapacity;
    private bool _hasParticleInstanceBuffer;

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
        _particleShader = new Shader(gl, ParticleVertexShaderSource, ParticleFragmentShaderSource);
        (_particleVao, _particleQuadVbo) = CreateParticleQuad();
        _particleShader.Bind();
        _particleShader.SetUniformInt("uTexture", 0);
        _particleShader.Unbind();
        BindSamplerUnits();
        BindDefaultPbrTextures();
        // DisableShadows also binds the default texture on unit 5, so no separate setup is needed.
        DisableShadows();
        // DisableIBL also binds the default cubemap/texture on units 6-8.
        DisableIBL();
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
        _shader.Unbind();
    }

    // Bind the 1×1 white texture to the optional PBR sampler units (2-4) once at construction.
    // Those samplers are statically used by the fragment shader (the gating `uHas*Map` uniform
    // doesn't make them un-referenced), so each must point at a *complete* texture for defined
    // behaviour. Draw only ever overwrites these units with a real map and never unbinds them,
    // so this one-time bind keeps the units complete for the renderer's lifetime — Draw can then
    // skip binding a fallback when a map is absent.
    private void BindDefaultPbrTextures()
    {
        foreach (
            var unit in (ReadOnlySpan<TextureUnit>)
                [TextureUnit.Texture2, TextureUnit.Texture3, TextureUnit.Texture4]
        )
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        }

        // Restore the default active unit so we don't leak Texture4 into later GL setup (e.g. the
        // Texture constructor binds without first selecting a unit).
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    // The shadow sampler (unit 5) is statically used by the fragment shader, so it must point at a
    // complete texture even when shadows are disabled. Bind the 1×1 white texture (sampled as depth
    // 1.0 = fully lit) until SetShadowMap swaps in a real depth map. As with the PBR fallbacks, the
    // shadow path never unbinds this unit, so the one-time bind keeps it valid for the lifetime.
    private void BindDefaultShadowTexture()
    {
        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>
    /// Binds the shadow map and uploads the light-space transform for the lighting pass. Call once
    /// per frame, after the shadow pass has populated the depth texture and before the draw loop.
    /// </summary>
    public void SetShadowMap(
        Matrix4x4 lightSpaceMatrix,
        uint depthTexture,
        float bias,
        bool enablePcf
    )
    {
        _shader.Bind();
        _shader.SetUniformMatrix4("uLightSpaceMatrix", lightSpaceMatrix);
        _shader.SetUniformFloat("uShadowBias", SanitizeNonNegative(bias));
        _shader.SetUniformInt("uUsePcf", enablePcf ? 1 : 0);
        _shader.SetUniformInt("uShadowsEnabled", 1);

        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, depthTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _shader.Unbind();
    }

    /// <summary>
    /// Disables shadow sampling: the lighting pass treats every fragment as fully lit. This is the
    /// default state until <see cref="SetShadowMap"/> is called.
    /// </summary>
    public void DisableShadows()
    {
        _shader.Bind();
        _shader.SetUniformInt("uShadowsEnabled", 0);
        _shader.SetUniformMatrix4("uLightSpaceMatrix", Matrix4x4.Identity);
        _shader.Unbind();

        // Restore the default (complete) shadow texture on unit 5. After a prior SetShadowMap the
        // unit may still point at a depth texture that gets deleted when its ShadowMapRenderer is
        // disposed, leaving the statically-used sampler incomplete even though sampling is gated off.
        BindDefaultShadowTexture();
    }

    // The IBL samplers (irradiance/prefiltered cubemaps, BRDF LUT) are statically used by the
    // fragment shader, so — like the PBR and shadow fallbacks above — they must point at complete
    // textures even when uUseIBL is 0. Bind the 1x1 white cubemap to units 6/7 and reuse the
    // existing 1x1 white 2D texture for unit 8 until SetEnvironmentMap swaps in real resources.
    private void BindDefaultIblTextures()
    {
        foreach (
            var unit in (ReadOnlySpan<TextureUnit>)[TextureUnit.Texture6, TextureUnit.Texture7]
        )
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.TextureCubeMap, _defaultCubemap);
        }

        _gl.ActiveTexture(TextureUnit.Texture8);
        _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>
    /// Binds a prefiltered <see cref="EnvironmentMap"/> (irradiance + prefiltered specular + BRDF
    /// LUT) and enables image-based lighting for the PBR path. Call once per frame, before the
    /// draw loop, whenever the scene has a skybox with a registered <see cref="EnvironmentMap"/>.
    /// Has no effect on the Blinn-Phong path.
    /// </summary>
    public void SetEnvironmentMap(EnvironmentMap environmentMap)
    {
        ArgumentNullException.ThrowIfNull(environmentMap);

        _shader.Bind();
        _shader.SetUniformInt("uUseIBL", 1);
        _shader.SetUniformFloat(
            "uMaxReflectionLod",
            Math.Max(environmentMap.PrefilteredMipCount - 1, 0)
        );

        _gl.ActiveTexture(TextureUnit.Texture6);
        _gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap.IrradianceMap);
        _gl.ActiveTexture(TextureUnit.Texture7);
        _gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap.PrefilteredMap);
        _gl.ActiveTexture(TextureUnit.Texture8);
        _gl.BindTexture(TextureTarget.Texture2D, environmentMap.BrdfLut);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _shader.Unbind();
    }

    /// <summary>
    /// Disables image-based lighting: the PBR path falls back to the flat constant ambient term
    /// used before this feature existed. This is the default state until
    /// <see cref="SetEnvironmentMap"/> is called; scenes without a skybox never need to call this
    /// explicitly.
    /// </summary>
    public void DisableIBL()
    {
        _shader.Bind();
        _shader.SetUniformInt("uUseIBL", 0);
        _shader.Unbind();

        // Restore the default (complete) IBL textures. After a prior SetEnvironmentMap the units
        // may still point at textures owned by an EnvironmentMap that has since been disposed,
        // leaving the statically-used samplers incomplete even though sampling is gated off
        // (mirrors BindDefaultShadowTexture's reasoning in DisableShadows).
        BindDefaultIblTextures();
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

    /// <summary>
    /// Enters the sorted blended draw pass shared by <see cref="MaterialBlendMode.Transparent"/>
    /// and <see cref="MaterialBlendMode.Additive"/> materials: depth testing stays on (so blended
    /// fragments are still occluded by opaque geometry already in the depth buffer) but depth
    /// <em>writes</em> are disabled (so blended fragments don't occlude each other or later opaque
    /// draws) and blending is enabled with the standard alpha blend func (<c>SrcAlpha</c>,
    /// <c>OneMinusSrcAlpha</c>) as the initial state. Each <see cref="Draw(GpuMesh,Matrix4x4,Matrix4x4,Material3D,TextureManager)"/>
    /// call within the pass switches the blend func to match its own material's
    /// <see cref="Material3D.BlendMode"/> (see <see cref="ApplyBlendFunc"/>), so Transparent and
    /// Additive draws can be interleaved in one back-to-front sorted sequence. Call after every
    /// opaque/cutout <see cref="Draw(GpuMesh,Matrix4x4,Matrix4x4,Material3D,TextureManager)"/>/
    /// <see cref="DrawInstanced"/> call for the frame, with entries sorted back-to-front (see
    /// <see cref="TransparencySorter"/>), and pair with <see cref="EndTransparentPass"/> once done.
    /// A scene that never calls this (no transparent/additive materials) renders exactly as before
    /// this pass existed.
    /// </summary>
    public void BeginTransparentPass()
    {
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    /// <summary>Restores depth writes and disables blending after the transparent pass.</summary>
    public void EndTransparentPass()
    {
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
    }

    /// <summary>
    /// Uploads scene-wide lighting uniforms. Call once per frame before the draw loop.
    /// </summary>
    public void SetSceneLighting(DirectionalLight light, Vector3 cameraPos)
    {
        var lenSq = light.Direction.LengthSquared();
        var dir =
            float.IsFinite(lenSq) && lenSq > 0f
                ? Vector3.Normalize(light.Direction)
                : Vector3.UnitY;
        var intensity = float.IsFinite(light.Intensity) ? MathF.Max(light.Intensity, 0f) : 0f;
        _shader.Bind();
        _shader.SetUniformVec3("uLightDir", dir);
        _shader.SetUniformVec4("uLightColor", light.Color.ToVector4());
        _shader.SetUniformFloat("uLightIntensity", intensity);
        _shader.SetUniformVec3("uCameraPos", cameraPos);
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the scene-wide ambient term used by the PBR path when image-based lighting is off.
    /// Call once per frame before the draw loop. The colour and intensity are combined here, so the
    /// shader reads a single pre-multiplied <c>vec3</c>.
    /// </summary>
    /// <remarks>
    /// Has no effect while an <see cref="EnvironmentMap"/> is bound via
    /// <see cref="SetEnvironmentMap"/> (IBL supplies its own, directional ambient), and none on the
    /// Blinn-Phong path, whose ambient is per-material — see <see cref="AmbientLight"/>.
    /// </remarks>
    public void SetAmbient(AmbientLight ambient)
    {
        var color = ambient.Color.ToVector4();
        var intensity = SanitizeNonNegative(ambient.Intensity);
        _shader.Bind();
        _shader.SetUniformVec3("uAmbientLight", new Vector3(color.X, color.Y, color.Z) * intensity);
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the active point lights for this frame. Call once per frame before the draw loop.
    /// At most <see cref="MaxPointLights"/> lights are used; any extras are ignored. Passing an
    /// empty span disables all point lights.
    /// </summary>
    public void SetPointLights(ReadOnlySpan<(Vector3 Position, PointLight Light)> lights)
    {
        var count = Math.Min(lights.Length, MaxPointLights);
        _shader.Bind();
        _shader.SetUniformInt("uPointLightCount", count);
        for (var i = 0; i < count; i++)
        {
            var (position, light) = lights[i];
            _shader.SetUniformVec3(PointPositionNames[i], position);
            _shader.SetUniformVec4(PointColorNames[i], light.Color.ToVector4());
            _shader.SetUniformFloat(PointIntensityNames[i], SanitizeNonNegative(light.Intensity));
            _shader.SetUniformFloat(PointRangeNames[i], SanitizeNonNegative(light.Range));
        }
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the active spot lights for this frame. Call once per frame before the draw loop.
    /// At most <see cref="MaxSpotLights"/> lights are used; any extras are ignored. Passing an
    /// empty span disables all spot lights.
    /// </summary>
    public void SetSpotLights(ReadOnlySpan<(Vector3 Position, SpotLight Light)> lights)
    {
        var count = Math.Min(lights.Length, MaxSpotLights);
        _shader.Bind();
        _shader.SetUniformInt("uSpotLightCount", count);
        for (var i = 0; i < count; i++)
        {
            var (position, light) = lights[i];

            var lenSq = light.Direction.LengthSquared();
            var direction =
                float.IsFinite(lenSq) && lenSq > 0f
                    ? Vector3.Normalize(light.Direction)
                    : -Vector3.UnitY;

            // Clamp angles to [0, pi] and force inner <= outer so the cos values stay ordered
            // (innerCos >= outerCos), which smoothstep requires for a well-defined cone edge.
            var outerAngle = Math.Clamp(SanitizeNonNegative(light.OuterConeAngle), 0f, MathF.PI);
            var innerAngle = Math.Clamp(SanitizeNonNegative(light.InnerConeAngle), 0f, outerAngle);

            _shader.SetUniformVec3(SpotPositionNames[i], position);
            _shader.SetUniformVec3(SpotDirectionNames[i], direction);
            _shader.SetUniformVec4(SpotColorNames[i], light.Color.ToVector4());
            _shader.SetUniformFloat(SpotIntensityNames[i], SanitizeNonNegative(light.Intensity));
            _shader.SetUniformFloat(SpotInnerCosNames[i], MathF.Cos(innerAngle));
            _shader.SetUniformFloat(SpotOuterCosNames[i], MathF.Cos(outerAngle));
            _shader.SetUniformFloat(SpotRangeNames[i], SanitizeNonNegative(light.Range));
        }
        _shader.Unbind();
    }

    // Guards against NaN/negative values leaking into shader uniforms (mirrors SetSceneLighting).
    private static float SanitizeNonNegative(float value) =>
        float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;

    /// <summary>Draws a single static mesh with the supplied transform and material.</summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    ) => DrawCore(mesh, model, viewProj, material, textures, skinned: false);

    /// <summary>
    /// Draws a single skinned mesh, uploading <paramref name="bonePalette"/> to the bone-matrix UBO
    /// and enabling GPU skinning in the vertex shader. Up to <see cref="MaxBones"/> matrices are used.
    /// </summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures,
        ReadOnlySpan<Matrix4x4> bonePalette
    )
    {
        SetBoneMatrices(bonePalette);
        DrawCore(mesh, model, viewProj, material, textures, skinned: true);
    }

    private void DrawCore(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures,
        bool skinned
    )
    {
        _shader.Bind();

        _shader.SetUniformInt("uSkinned", skinned ? 1 : 0);
        _shader.SetUniformInt("uInstanced", 0);

        _shader.SetUniformMatrix4("uModel", model);
        _shader.SetUniformMatrix4("uViewProj", viewProj);

        if (!Matrix4x4.Invert(model, out var invModel))
            invModel = Matrix4x4.Identity;
        _shader.SetUniformMatrix3("uNormalMatrix", Matrix4x4.Transpose(invModel));

        ApplyBlendFunc(material.BlendMode);
        BindMaterial(material, textures);

        mesh.Draw();
        DrawCallCount++;

        _shader.Unbind();
    }

    // Selects the blend func for the pass entered by BeginTransparentPass, per-draw, so a single
    // sorted pass can interleave Transparent and Additive materials (see TransparencySorter). A
    // no-op for Opaque/Cutout: blending is disabled outside the transparent pass, so the blend func
    // has no visible effect there regardless — this only matters for the two blended modes.
    private void ApplyBlendFunc(MaterialBlendMode blendMode)
    {
        switch (blendMode)
        {
            case MaterialBlendMode.Additive:
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
            case MaterialBlendMode.Transparent:
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
        }
    }

    /// <summary>
    /// Draws every particle in <paramref name="instances"/> as a camera-facing billboard, in a
    /// single <c>glDrawArraysInstanced</c> call — one emitter's particles, drawn once. Each
    /// billboard is built from <paramref name="cameraRight"/>/<paramref name="cameraUp"/> (see
    /// <see cref="BillboardMath.ExtractCameraAxes"/>) so it faces the camera however it's oriented.
    /// Call inside a <see cref="BeginTransparentPass"/>/<see cref="EndTransparentPass"/> bracket
    /// (depth test on, depth write off, blending enabled): <paramref name="blendMode"/> switches
    /// the blend func per-call exactly as the mesh transparent pass does (<see cref="ApplyBlendFunc"/>),
    /// so Transparent and Additive emitters can be interleaved with each other in one sorted
    /// sequence. Unlit — particles don't read scene lighting/shadows. No-op for an empty span.
    /// </summary>
    internal unsafe void DrawParticles(
        ReadOnlySpan<ParticleInstanceData> instances,
        Vector3 cameraRight,
        Vector3 cameraUp,
        Matrix4x4 viewProj,
        string? texturePath,
        TextureManager textures,
        MaterialBlendMode blendMode
    )
    {
        if (instances.IsEmpty)
            return;

        ApplyBlendFunc(blendMode);

        _particleShader.Bind();
        _particleShader.SetUniformMatrix4("uViewProj", viewProj);
        _particleShader.SetUniformVec3("uCameraRight", cameraRight);
        _particleShader.SetUniformVec3("uCameraUp", cameraUp);

        // Select the unit before Get so a first-time Texture ctor binds on this unit rather than
        // clobbering whichever unit happened to be active (mirrors BindMaterial's reasoning).
        _gl.ActiveTexture(TextureUnit.Texture0);
        if (!string.IsNullOrEmpty(texturePath))
            textures.Get(texturePath).Bind(TextureUnit.Texture0);
        else
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        EnsureParticleInstanceCapacity(instances.Length);

        _gl.BindVertexArray(_particleVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _particleInstanceVbo);
        fixed (ParticleInstanceData* ptr = instances)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(instances.Length * sizeof(ParticleInstanceData)),
                ptr
            );
        }

        _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)instances.Length);
        _gl.BindVertexArray(0);
        DrawCallCount++;

        _particleShader.Unbind();
    }

    private unsafe (uint Vao, uint Vbo) CreateParticleQuad()
    {
        var vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        var vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* ptr = ParticleQuadVertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(ParticleQuadVertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw
            );
        }

        const uint stride = 4 * sizeof(float);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(2 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
        return (vao, vbo);
    }

    // Lazily creates the particle instance VBO on first use and grows it (doubling) whenever a draw
    // needs more capacity than it currently has — mirrors GpuMesh.EnsureInstanceCapacity exactly,
    // just against ParticleInstanceData's layout instead of InstanceData's.
    private unsafe void EnsureParticleInstanceCapacity(int count)
    {
        if (_hasParticleInstanceBuffer && count <= _particleInstanceCapacity)
            return;

        _gl.BindVertexArray(_particleVao);

        if (!_hasParticleInstanceBuffer)
        {
            _particleInstanceVbo = _gl.GenBuffer();
            _hasParticleInstanceBuffer = true;
        }

        _particleInstanceCapacity = Math.Max(count, Math.Max(_particleInstanceCapacity * 2, 64));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _particleInstanceVbo);
        _gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(_particleInstanceCapacity * sizeof(ParticleInstanceData)),
            null,
            BufferUsageARB.DynamicDraw
        );

        var stride = (uint)sizeof(ParticleInstanceData);
        _gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribDivisor(2, 1);

        _gl.VertexAttribPointer(
            3,
            2,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(3 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribDivisor(3, 1);

        _gl.VertexAttribPointer(
            4,
            1,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(5 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribDivisor(4, 1);

        _gl.VertexAttribPointer(
            5,
            4,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(6 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribDivisor(5, 1);

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws <paramref name="models"/> copies of <paramref name="mesh"/>, all sharing
    /// <paramref name="material"/>, in a single instanced draw call. Skinned meshes always use the
    /// per-entity <see cref="Draw(GpuMesh, Matrix4x4, Matrix4x4, Material3D, TextureManager, ReadOnlySpan{Matrix4x4})"/>
    /// overload instead — bone palettes are per-entity state, so they can't be folded into per-instance
    /// attributes here. No-op for an empty span.
    /// </summary>
    public void DrawInstanced(
        GpuMesh mesh,
        ReadOnlySpan<Matrix4x4> models,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    )
    {
        if (models.IsEmpty)
            return;

        _shader.Bind();

        _shader.SetUniformInt("uSkinned", 0);
        _shader.SetUniformInt("uInstanced", 1);
        _shader.SetUniformMatrix4("uViewProj", viewProj);

        BindMaterial(material, textures);

        EnsureInstanceScratchCapacity(models.Length);
        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            if (!Matrix4x4.Invert(model, out var invModel))
                invModel = Matrix4x4.Identity;
            _instanceScratch![i] = new InstanceData(model, Matrix4x4.Transpose(invModel));
        }

        mesh.DrawInstanced(_instanceScratch.AsSpan(0, models.Length));
        DrawCallCount++;

        _shader.Unbind();
    }

    private InstanceData[]? _instanceScratch;

    // Grown by doubling (not per-draw) so a stable-sized scene's instance groups don't reallocate
    // once warmed up, mirroring the _pointLights/_spotLights scratch buffers in MeshRenderSystem.
    private void EnsureInstanceScratchCapacity(int count)
    {
        if (_instanceScratch != null && _instanceScratch.Length >= count)
            return;

        var capacity = Math.Max(count, Math.Max((_instanceScratch?.Length ?? 0) * 2, 64));
        _instanceScratch = new InstanceData[capacity];
    }

    // Uploads every material/texture uniform shared by the instanced and non-instanced draw paths;
    // the caller is responsible for uModel/uNormalMatrix/uSkinned/uInstanced, which differ between them.
    private void BindMaterial(Material3D material, TextureManager textures)
    {
        _shader.SetUniformVec4("uDiffuseColor", material.Diffuse.ToVector4());
        _shader.SetUniformVec4("uAmbientColor", material.Ambient.ToVector4());
        _shader.SetUniformVec4("uSpecularColor", material.Specular.ToVector4());
        _shader.SetUniformFloat(
            "uShininess",
            float.IsFinite(material.Shininess) ? MathF.Max(material.Shininess, 1f) : 1f
        );

        var metallic = float.IsFinite(material.MetallicFactor)
            ? Math.Clamp(material.MetallicFactor, 0f, 1f)
            : 1f;
        var roughness = float.IsFinite(material.RoughnessFactor)
            ? Math.Clamp(material.RoughnessFactor, 0f, 1f)
            : 1f;

        _shader.SetUniformInt("uUsePbr", material.UsePbr ? 1 : 0);
        _shader.SetUniformFloat("uMetallicFactor", metallic);
        _shader.SetUniformFloat("uRoughnessFactor", roughness);
        _shader.SetUniformVec4("uEmissiveColor", material.EmissiveColor.ToVector4());
        _shader.SetUniformFloat(
            "uEmissiveIntensity",
            float.IsFinite(material.EmissiveIntensity)
                ? MathF.Max(material.EmissiveIntensity, 0f)
                : 1f
        );

        var opacity = float.IsFinite(material.Opacity) ? Math.Clamp(material.Opacity, 0f, 1f) : 1f;
        var alphaCutoff = float.IsFinite(material.AlphaCutoff)
            ? Math.Clamp(material.AlphaCutoff, 0f, 1f)
            : 0.5f;
        _shader.SetUniformFloat("uOpacity", opacity);
        _shader.SetUniformInt("uBlendMode", (int)material.BlendMode);
        _shader.SetUniformFloat("uAlphaCutoff", alphaCutoff);

        // Select the target unit *before* TextureManager.Get: a first-time Get constructs a
        // Texture whose ctor binds on the currently-active unit, which would otherwise clobber a
        // previously-bound unit. Activating first keeps that side-effect bind on the right unit.
        _gl.ActiveTexture(TextureUnit.Texture0);
        if (!string.IsNullOrEmpty(material.DiffuseTexturePath))
            textures.Get(material.DiffuseTexturePath).Bind(TextureUnit.Texture0);
        else
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        _gl.ActiveTexture(TextureUnit.Texture1);
        if (!string.IsNullOrEmpty(material.NormalTexturePath))
        {
            textures.Get(material.NormalTexturePath).Bind(TextureUnit.Texture1);
            _shader.SetUniformInt("uHasNormalMap", 1);
        }
        else
        {
            _gl.BindTexture(TextureTarget.Texture2D, _defaultNormalTexture);
            _shader.SetUniformInt("uHasNormalMap", 0);
        }

        // Only the PBR branch samples the metallic-roughness/AO/emissive maps, so skip the
        // texture binds entirely for Blinn-Phong materials and just clear the has-flags.
        if (material.UsePbr)
        {
            BindOptionalTexture(
                textures,
                material.MetallicRoughnessTexturePath,
                TextureUnit.Texture2,
                "uHasMetallicRoughnessMap"
            );
            BindOptionalTexture(
                textures,
                material.AoTexturePath,
                TextureUnit.Texture3,
                "uHasAoMap"
            );
            BindOptionalTexture(
                textures,
                material.EmissiveTexturePath,
                TextureUnit.Texture4,
                "uHasEmissiveMap"
            );
        }
        else
        {
            _shader.SetUniformInt("uHasMetallicRoughnessMap", 0);
            _shader.SetUniformInt("uHasAoMap", 0);
            _shader.SetUniformInt("uHasEmissiveMap", 0);
        }
    }

    // Binds an optional PBR texture to the given unit and flags its presence. When the path is
    // empty the bind is skipped: the unit already holds a complete fallback texture from
    // construction (see BindDefaultPbrTextures), so the (uniform-gated) sampler stays valid and
    // `uHas*Map = 0` tells the shader to ignore it. Sampler-to-unit assignments are likewise set
    // once at construction (see BindSamplerUnits), so they aren't re-uploaded here.
    private void BindOptionalTexture(
        TextureManager textures,
        string? path,
        TextureUnit unit,
        string hasUniform
    )
    {
        if (!string.IsNullOrEmpty(path))
        {
            // Select the unit before Get so a first-time Texture ctor binds on this unit rather
            // than clobbering whichever unit happened to be active.
            _gl.ActiveTexture(unit);
            textures.Get(path).Bind(unit);
            _shader.SetUniformInt(hasUniform, 1);
        }
        else
        {
            _shader.SetUniformInt(hasUniform, 0);
        }
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

    // Flat normal map: (0.5, 0.5, 1.0) encodes tangent-space normal pointing straight up.
    private unsafe uint CreateFlatNormalTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] flatNormal = [128, 128, 255, 255];
        fixed (byte* ptr = flatNormal)
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

    // 1x1 white cubemap: a complete fallback for the IBL cubemap samplers when no EnvironmentMap
    // is bound (mirrors CreateWhiteTexture's role for the 2D PBR/shadow fallbacks).
    private unsafe uint CreateWhiteCubemap()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, handle);
        byte[] white = [255, 255, 255, 255];
        TextureTarget[] faces =
        [
            TextureTarget.TextureCubeMapPositiveX,
            TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY,
            TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ,
            TextureTarget.TextureCubeMapNegativeZ,
        ];
        fixed (byte* ptr = white)
        {
            foreach (var face in faces)
            {
                _gl.TexImage2D(
                    face,
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
        return handle;
    }

    // Allocates the bone-matrix uniform buffer (MaxBones mat4s) and links it to the shader's "Bones"
    // block via a shared binding point. Filled per skinned draw by SetBoneMatrices.
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

    /// <summary>
    /// Uploads a skinning matrix palette to the bone UBO. At most <see cref="MaxBones"/> matrices are
    /// used; extras are ignored. The skinned <see cref="Draw(GpuMesh, Matrix4x4, Matrix4x4, Material3D, TextureManager, ReadOnlySpan{Matrix4x4})"/>
    /// overload calls this for you.
    /// </summary>
    public unsafe void SetBoneMatrices(ReadOnlySpan<Matrix4x4> palette)
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
        _gl.DeleteTexture(_defaultTexture);
        _gl.DeleteTexture(_defaultNormalTexture);
        _gl.DeleteTexture(_defaultCubemap);
        _gl.DeleteBuffer(_boneUbo);

        _particleShader.Dispose();
        _gl.DeleteVertexArray(_particleVao);
        _gl.DeleteBuffer(_particleQuadVbo);
        if (_hasParticleInstanceBuffer)
            _gl.DeleteBuffer(_particleInstanceVbo);
    }
}

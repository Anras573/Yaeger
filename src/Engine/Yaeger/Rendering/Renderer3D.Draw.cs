using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

// Static-mesh draw path: single-mesh Draw, instanced DrawInstanced, the shared material/texture
// binding they both use, and the transparent-pass blend state they draw within. See
// Renderer3D.Skinning.cs for the (separate) skinned and instanced-skinned draw paths, and
// Renderer3D.Particles.cs for billboard particles.
public sealed partial class Renderer3D
{
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
    // Shared with the instanced-skinned path in Renderer3D.Skinning.cs (DrawSkinnedChunk), which
    // fills the same scratch buffer with its own per-chunk instance data.
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
}

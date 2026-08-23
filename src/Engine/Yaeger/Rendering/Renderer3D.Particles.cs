using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

// Billboard particle rendering: a separate unlit shader/VAO from the lit Blinn-Phong/PBR mesh path
// (see Renderer3D.Draw.cs and Renderer3D.Skinning.cs) — same reasoning as SkyboxRenderer/UiRenderer
// staying outside Renderer3D's main shader. Includes the soft-particle scene depth sample.
public sealed partial class Renderer3D
{
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

    private readonly Shader _particleShader;
    private readonly uint _particleVao;
    private readonly uint _particleQuadVbo;
    private uint _particleInstanceVbo;
    private int _particleInstanceCapacity;
    private bool _hasParticleInstanceBuffer;

    // Soft-particle scene depth: rebound on every DrawParticles call (see DrawParticles), since
    // unit 9 is otherwise untouched by anything else but re-binding defensively costs nothing and
    // keeps this robust regardless of what runs between SetSceneDepth and the next particle draw.
    private uint _sceneDepthTexture;

    /// <summary>
    /// Binds a depth texture holding the opaque scene's depth (e.g. from a <see cref="RenderTarget"/>
    /// constructed with a depth attachment) for the particle pass's soft-particle fade — see
    /// <see cref="Graphics.ParticleEmitter3D.SoftFade"/>. Call once per frame, after the opaque pass
    /// has written that texture's depth and before <see cref="DrawParticles"/>.
    /// </summary>
    /// <param name="depthTexture">A complete depth texture matching the camera this frame renders with.</param>
    /// <param name="near">The camera's near plane — see <see cref="Graphics.Camera3D.Near"/>.</param>
    /// <param name="far">The camera's far plane — see <see cref="Graphics.Camera3D.Far"/>.</param>
    /// <param name="viewportWidth">Current viewport width in pixels, for mapping a fragment's screen position to a depth-texture UV.</param>
    /// <param name="viewportHeight">Current viewport height in pixels.</param>
    public void SetSceneDepth(
        uint depthTexture,
        float near,
        float far,
        int viewportWidth,
        int viewportHeight
    )
    {
        _sceneDepthTexture = depthTexture;

        var n = near > 0f ? near : 0.0001f;
        var f = far > n ? far : n + 1f;
        var width = viewportWidth > 0 ? viewportWidth : 1;
        var height = viewportHeight > 0 ? viewportHeight : 1;

        _particleShader.Bind();
        _particleShader.SetUniformInt("uSoftFadeSceneAvailable", 1);
        _particleShader.SetUniformFloat("uNear", n);
        _particleShader.SetUniformFloat("uFar", f);
        _particleShader.SetUniformVec2("uInvViewportSize", new Vector2(1f / width, 1f / height));
        _particleShader.Unbind();
    }

    /// <summary>
    /// Disables the soft-particle scene depth sample: every particle fades exactly as it did before
    /// this feature existed (a hard depth-test cutoff, no fade), regardless of each emitter's
    /// <see cref="Graphics.ParticleEmitter3D.SoftFade"/>. This is the default state until
    /// <see cref="SetSceneDepth"/> is called; exists so a shared <see cref="Renderer3D"/> doesn't
    /// leak a previous scene's depth texture into one that doesn't supply its own (mirrors
    /// <see cref="DisableShadows"/>/<see cref="DisableIBL"/>/<see cref="DisableFog"/>).
    /// </summary>
    public void DisableSceneDepth()
    {
        _sceneDepthTexture = _defaultTexture;

        _particleShader.Bind();
        _particleShader.SetUniformInt("uSoftFadeSceneAvailable", 0);
        _particleShader.Unbind();
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
    /// <param name="softFadeDistance">
    /// This emitter's <see cref="Graphics.ParticleEmitter3D.SoftFade"/> — world units over which a
    /// particle fades out as it nears scene geometry behind it. 0 (the default when an emitter never
    /// sets it) disables the fade regardless of whether <see cref="SetSceneDepth"/> was called.
    /// </param>
    internal unsafe void DrawParticles(
        ReadOnlySpan<ParticleInstanceData> instances,
        Vector3 cameraRight,
        Vector3 cameraUp,
        Matrix4x4 viewProj,
        string? texturePath,
        TextureManager textures,
        MaterialBlendMode blendMode,
        float softFadeDistance = 0f
    )
    {
        if (instances.IsEmpty)
            return;

        ApplyBlendFunc(blendMode);

        _particleShader.Bind();
        _particleShader.SetUniformMatrix4("uViewProj", viewProj);
        _particleShader.SetUniformVec3("uCameraRight", cameraRight);
        _particleShader.SetUniformVec3("uCameraUp", cameraUp);
        _particleShader.SetUniformFloat(
            "uSoftFadeDistance",
            float.IsFinite(softFadeDistance) ? MathF.Max(softFadeDistance, 0f) : 0f
        );

        // Select the unit before Get so a first-time Texture ctor binds on this unit rather than
        // clobbering whichever unit happened to be active (mirrors BindMaterial's reasoning).
        _gl.ActiveTexture(TextureUnit.Texture0);
        if (!string.IsNullOrEmpty(texturePath))
            textures.Get(texturePath).Bind(TextureUnit.Texture0);
        else
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        // Re-bound on every draw (unlike the PBR/shadow/IBL fallbacks, which are bound once at
        // construction and never touched again): unit 9 is exclusive to the particle shader today,
        // but re-binding defensively here costs nothing and keeps this correct even if that stops
        // being true later.
        _gl.ActiveTexture(TextureUnit.Texture9);
        _gl.BindTexture(TextureTarget.Texture2D, _sceneDepthTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);

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

        _gl.VertexAttribPointer(
            6,
            4,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)(10 * sizeof(float))
        );
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribDivisor(6, 1);

        _gl.BindVertexArray(0);
    }
}

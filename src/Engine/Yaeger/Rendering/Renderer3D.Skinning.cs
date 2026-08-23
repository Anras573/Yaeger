using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

// GPU skinning: the fixed-size bone-matrix UBO used by the immediate skinned Draw overload (see
// Renderer3D.Draw.cs), and the texture-buffer-backed bone palette + chunking used by
// DrawInstancedSkinned for crowds whose combined palettes exceed the UBO's 128-bone cap.
public sealed partial class Renderer3D
{
    /// <summary>Maximum number of bones the vertex shader's skinning palette can hold (matches MAX_BONES in GLSL).</summary>
    public const int MaxBones = 128;

    // Binding point linking the "Bones" uniform block to the bone-matrix UBO. Arbitrary but must not
    // collide with any other uniform block binding (the renderer has none).
    private const uint BoneBlockBinding = 0;

    // Texture unit the instanced-skinning bone-palette buffer texture (uBonePalette, a samplerBuffer)
    // is bound to, once at construction and never touched again — see CreateBonePaletteTextureBuffer.
    // Past every other sampler this renderer uses (0-9 the material/shadow/IBL/scene-depth samplers,
    // 10-11 the point shadow cubemap slots).
    private const int BonePaletteTextureUnit = 12;

    private readonly uint _boneUbo;
    private readonly uint _bonePaletteBuffer;
    private readonly uint _bonePaletteTexture;
    private int _bonePaletteCapacityTexels;

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

    // Allocates the bone-palette texture buffer: a plain GL buffer object (grown by
    // EnsureBonePaletteCapacity, same doubling pattern as the instance scratch buffers) whose store
    // is aliased by a GL_TEXTURE_BUFFER texture, sampled in the vertex shader as `uBonePalette`
    // (a samplerBuffer, texelFetch-indexed) instead of the fixed 128-matrix UBO the immediate skinned
    // draw path uses — see docs/instancing.md for why instanced skinning needs this instead of the
    // UBO. Bound to BonePaletteTextureUnit once here and never touched again: resizing the buffer's
    // store later (in EnsureBonePaletteCapacity) doesn't change which texture object is bound to the
    // unit, only what it aliases.
    private unsafe (uint Buffer, uint Texture) CreateBonePaletteTextureBuffer()
    {
        var buffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.TextureBuffer, buffer);
        _gl.BufferData(BufferTargetARB.TextureBuffer, 0, null, BufferUsageARB.DynamicDraw);
        _gl.BindBuffer(BufferTargetARB.TextureBuffer, 0);

        var texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0 + BonePaletteTextureUnit);
        _gl.BindTexture(TextureTarget.TextureBuffer, texture);
        _gl.TexBuffer(TextureTarget.TextureBuffer, SizedInternalFormat.Rgba32fArb, buffer);
        _gl.ActiveTexture(TextureUnit.Texture0);

        return (buffer, texture);
    }

    // Grows the bone-palette buffer's store (doubling) whenever an instanced-skinned draw needs more
    // texel capacity than it currently has. Re-issues TexBuffer after resizing: the buffer object's
    // handle doesn't change, but re-attaching defensively costs nothing and keeps this robust
    // regardless of driver behaviour around a texture buffer's aliased store being resized in place.
    private unsafe void EnsureBonePaletteCapacity(int texelCount)
    {
        if (texelCount <= _bonePaletteCapacityTexels)
            return;

        _bonePaletteCapacityTexels = Math.Max(
            texelCount,
            Math.Max(_bonePaletteCapacityTexels * 2, 256)
        );

        _gl.BindBuffer(BufferTargetARB.TextureBuffer, _bonePaletteBuffer);
        _gl.BufferData(
            BufferTargetARB.TextureBuffer,
            (nuint)(_bonePaletteCapacityTexels * sizeof(Vector4)),
            null,
            BufferUsageARB.DynamicDraw
        );
        _gl.BindBuffer(BufferTargetARB.TextureBuffer, 0);

        _gl.ActiveTexture(TextureUnit.Texture0 + BonePaletteTextureUnit);
        _gl.BindTexture(TextureTarget.TextureBuffer, _bonePaletteTexture);
        _gl.TexBuffer(
            TextureTarget.TextureBuffer,
            SizedInternalFormat.Rgba32fArb,
            _bonePaletteBuffer
        );
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    private unsafe void UploadBonePaletteTexels(ReadOnlySpan<Vector4> texels)
    {
        EnsureBonePaletteCapacity(texels.Length);

        _gl.BindBuffer(BufferTargetARB.TextureBuffer, _bonePaletteBuffer);
        fixed (Vector4* ptr = texels)
        {
            _gl.BufferSubData(
                BufferTargetARB.TextureBuffer,
                0,
                (nuint)(texels.Length * sizeof(Vector4)),
                ptr
            );
        }
        _gl.BindBuffer(BufferTargetARB.TextureBuffer, 0);
    }

    private int[]? _boneCountScratch;
    private float[]? _paletteOffsetScratch;
    private Vector4[]? _boneTexelScratch;

    /// <summary>
    /// Draws <paramref name="models"/>.Length instances of <paramref name="mesh"/>, all sharing
    /// <paramref name="material"/> and skinned by the parallel entry in <paramref name="bonePalettes"/>,
    /// in one or more instanced draw calls — see <see cref="InstancedSkinningPlanner"/> for when more
    /// than one call happens (only for a group whose combined palettes exceed the texel cap; a group
    /// under it always draws in exactly one call). <paramref name="bonePalettes"/>[i] is instance i's
    /// own resolved skinning matrices and <paramref name="paletteOffsets"/>[i] its cumulative starting
    /// bone-slot offset — both as produced by <see cref="MeshInstanceBatcher.AddSkinned"/>; all three
    /// spans/lists must be the same length as <paramref name="models"/>. Bone palettes are uploaded to
    /// a texture buffer rather than the fixed <see cref="MaxBones"/> UBO the immediate skinned
    /// <see cref="Draw(GpuMesh,Matrix4x4,Matrix4x4,Material3D,TextureManager,ReadOnlySpan{Matrix4x4})"/>
    /// overload uses, so per-instance palettes aren't capped at 128 bones each. No-op for an empty span.
    /// </summary>
    public void DrawInstancedSkinned(
        GpuMesh mesh,
        ReadOnlySpan<Matrix4x4> models,
        IReadOnlyList<Matrix4x4[]> bonePalettes,
        IReadOnlyList<int> paletteOffsets,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    )
    {
        if (models.IsEmpty)
            return;

        if (_boneCountScratch == null || _boneCountScratch.Length < models.Length)
            _boneCountScratch = new int[Math.Max(models.Length, 64)];
        for (var i = 0; i < models.Length; i++)
            _boneCountScratch[i] = bonePalettes[i].Length;

        var chunks = InstancedSkinningPlanner.PlanChunks(
            new ArraySegment<int>(_boneCountScratch, 0, models.Length)
        );

        _shader.Bind();
        _shader.SetUniformInt("uSkinned", 1);
        _shader.SetUniformInt("uInstanced", 1);
        _shader.SetUniformMatrix4("uViewProj", viewProj);
        BindMaterial(material, textures);

        foreach (var (start, count) in chunks)
            DrawSkinnedChunk(mesh, models, bonePalettes, paletteOffsets, start, count);

        _shader.Unbind();
    }

    // Draws one chunk (a contiguous instance range small enough to fit the bone-palette texel cap —
    // see InstancedSkinningPlanner) of an instanced-skinned group. Rebases each instance's group-wide
    // PaletteOffsets entry to be relative to this chunk's own palette buffer, which always starts
    // packing at texel 0 regardless of where the chunk sits within the full group.
    private unsafe void DrawSkinnedChunk(
        GpuMesh mesh,
        ReadOnlySpan<Matrix4x4> models,
        IReadOnlyList<Matrix4x4[]> bonePalettes,
        IReadOnlyList<int> paletteOffsets,
        int start,
        int count
    )
    {
        EnsureInstanceScratchCapacity(count);
        if (_paletteOffsetScratch == null || _paletteOffsetScratch.Length < count)
            _paletteOffsetScratch = new float[Math.Max(count, 64)];

        var baseOffset = paletteOffsets[start];
        for (var i = 0; i < count; i++)
        {
            var model = models[start + i];
            if (!Matrix4x4.Invert(model, out var invModel))
                invModel = Matrix4x4.Identity;
            _instanceScratch![i] = new InstanceData(model, Matrix4x4.Transpose(invModel));
            _paletteOffsetScratch[i] = paletteOffsets[start + i] - baseOffset;
        }

        var texelTotal = BonePaletteBuffer.TotalTexels(bonePalettes, start, count);
        if (_boneTexelScratch == null || _boneTexelScratch.Length < texelTotal)
            _boneTexelScratch = new Vector4[Math.Max(texelTotal, 256)];
        BonePaletteBuffer.Pack(bonePalettes, start, count, _boneTexelScratch.AsSpan(0, texelTotal));
        UploadBonePaletteTexels(_boneTexelScratch.AsSpan(0, texelTotal));

        mesh.DrawInstancedSkinned(
            _instanceScratch.AsSpan(0, count),
            _paletteOffsetScratch.AsSpan(0, count)
        );
        DrawCallCount++;
    }
}

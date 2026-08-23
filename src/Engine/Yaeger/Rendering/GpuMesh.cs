using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public sealed class GpuMesh : IDisposable
{
    // Instance attributes start right after the six per-vertex attributes GpuMesh's constructor sets
    // up below (locations 0-5): a mat4 model matrix consumes locations 6-9 (one vec4 column each),
    // followed by a mat3 normal matrix at locations 10-12. Both are read with VertexAttribDivisor 1
    // so they advance once per instance instead of once per vertex.
    private const uint InstanceModelLocation = 6;
    private const uint InstanceNormalMatrixLocation = 10;

    // Per-instance bone-palette base offset for instanced skinning (see
    // Renderer3D.DrawInstancedSkinned): one float, read with VertexAttribDivisor 1 like the instance
    // transform attributes above, but backed by its own buffer since it's only ever populated
    // alongside a skinned instanced draw (DrawInstancedSkinned), never the static one (DrawInstanced).
    private const uint InstancePaletteOffsetLocation = 13;

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly Buffer<Vertex3D> _vbo;
    private readonly Buffer<uint> _ebo;
    private readonly uint _indexCount;
    private uint _instanceVbo;
    private int _instanceCapacity;
    private bool _hasInstanceBuffer;
    private uint _paletteOffsetVbo;
    private int _paletteOffsetCapacity;
    private bool _hasPaletteOffsetBuffer;

    public unsafe GpuMesh(GL gl, MeshData data)
    {
        _gl = gl;
        _indexCount = (uint)data.Indices.Length;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = new Buffer<Vertex3D>(gl, data.Vertices, BufferTargetARB.ArrayBuffer);
        _vbo.Bind();
        _ebo = new Buffer<uint>(gl, data.Indices, BufferTargetARB.ElementArrayBuffer);
        _ebo.Bind();

        var stride = (uint)sizeof(Vertex3D);
        SetupAttrib(0, 3, stride, OffsetOf(nameof(Vertex3D.Position)));
        SetupAttrib(1, 3, stride, OffsetOf(nameof(Vertex3D.Normal)));
        SetupAttrib(2, 2, stride, OffsetOf(nameof(Vertex3D.TexCoord)));
        SetupAttrib(3, 3, stride, OffsetOf(nameof(Vertex3D.Tangent)));
        // Skinning attributes; zero for static meshes (ignored when uSkinned == 0 in the shader).
        SetupAttrib(4, 4, stride, OffsetOf(nameof(Vertex3D.BoneIndices)));
        SetupAttrib(5, 4, stride, OffsetOf(nameof(Vertex3D.BoneWeights)));

        _gl.BindVertexArray(0);
    }

    private static uint OffsetOf(string fieldName) =>
        (uint)(nint)Marshal.OffsetOf<Vertex3D>(fieldName);

    private unsafe void SetupAttrib(uint index, int count, uint stride, uint offset)
    {
        _gl.VertexAttribPointer(
            index,
            count,
            VertexAttribPointerType.Float,
            false,
            stride,
            (void*)offset
        );
        _gl.EnableVertexAttribArray(index);
    }

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(
            PrimitiveType.Triangles,
            _indexCount,
            DrawElementsType.UnsignedInt,
            (void*)0
        );
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws <paramref name="instances"/> copies of this mesh in a single <c>glDrawElementsInstanced</c>
    /// call, streaming each instance's model/normal matrix through a per-mesh instance buffer. No-op
    /// for an empty span. Internal: <see cref="InstanceData"/> is engine-internal plumbing shared
    /// between <see cref="Renderer3D"/> and <see cref="ShadowMapRenderer"/>, the only callers.
    /// </summary>
    internal unsafe void DrawInstanced(ReadOnlySpan<InstanceData> instances)
    {
        if (instances.IsEmpty)
            return;

        EnsureInstanceCapacity(instances.Length);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        fixed (InstanceData* ptr = instances)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(instances.Length * sizeof(InstanceData)),
                ptr
            );
        }

        _gl.DrawElementsInstanced(
            PrimitiveType.Triangles,
            _indexCount,
            DrawElementsType.UnsignedInt,
            (void*)0,
            (uint)instances.Length
        );
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws <paramref name="transforms"/>.Length copies of this mesh in a single
    /// <c>glDrawElementsInstanced</c> call with GPU skinning enabled, streaming each instance's model/
    /// normal matrix (identically to <see cref="DrawInstanced"/>) plus its bone-palette base offset —
    /// an index into <see cref="Renderer3D"/>'s bone-palette texture buffer, read by
    /// <c>aInstancePaletteBase</c> in <c>Renderer3D.vert</c>. <paramref name="paletteOffsets"/> must
    /// be the same length as <paramref name="transforms"/>. No-op for an empty span. Internal: shared
    /// plumbing between <see cref="Renderer3D"/> and <see cref="ShadowMapRenderer"/>, the only callers.
    /// </summary>
    internal unsafe void DrawInstancedSkinned(
        ReadOnlySpan<InstanceData> transforms,
        ReadOnlySpan<float> paletteOffsets
    )
    {
        if (transforms.IsEmpty)
            return;

        EnsureInstanceCapacity(transforms.Length);
        EnsurePaletteOffsetCapacity(paletteOffsets.Length);

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        fixed (InstanceData* ptr = transforms)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(transforms.Length * sizeof(InstanceData)),
                ptr
            );
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _paletteOffsetVbo);
        fixed (float* ptr = paletteOffsets)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(paletteOffsets.Length * sizeof(float)),
                ptr
            );
        }

        _gl.DrawElementsInstanced(
            PrimitiveType.Triangles,
            _indexCount,
            DrawElementsType.UnsignedInt,
            (void*)0,
            (uint)transforms.Length
        );
        _gl.BindVertexArray(0);
    }

    // Lazily creates the palette-offset VBO on first use and grows it (doubling, orphaning the old
    // store) whenever a skinned instanced draw needs more capacity than it currently has — mirrors
    // EnsureInstanceCapacity exactly, just against a single-float attribute instead of InstanceData's.
    private unsafe void EnsurePaletteOffsetCapacity(int count)
    {
        if (_hasPaletteOffsetBuffer && count <= _paletteOffsetCapacity)
            return;

        _gl.BindVertexArray(_vao);

        if (!_hasPaletteOffsetBuffer)
        {
            _paletteOffsetVbo = _gl.GenBuffer();
            _hasPaletteOffsetBuffer = true;
        }

        _paletteOffsetCapacity = Math.Max(count, Math.Max(_paletteOffsetCapacity * 2, 64));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _paletteOffsetVbo);
        _gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(_paletteOffsetCapacity * sizeof(float)),
            null,
            BufferUsageARB.DynamicDraw
        );

        _gl.VertexAttribPointer(
            InstancePaletteOffsetLocation,
            1,
            VertexAttribPointerType.Float,
            false,
            sizeof(float),
            (void*)0
        );
        _gl.EnableVertexAttribArray(InstancePaletteOffsetLocation);
        _gl.VertexAttribDivisor(InstancePaletteOffsetLocation, 1);

        _gl.BindVertexArray(0);
    }

    // Lazily creates the instance VBO on first use and grows it (doubling, orphaning the old store)
    // whenever a draw needs more capacity than it currently has. Vertex attribute pointers only need
    // to be (re-)established when the buffer handle itself changes or grows — VertexAttribPointer
    // binds to the *currently bound buffer*, so re-pointing after every BufferData call on the same
    // handle would be redundant but is done anyway here for simplicity since it's a rare, amortized
    // (doubling) cost, not a per-draw one.
    private unsafe void EnsureInstanceCapacity(int count)
    {
        if (_hasInstanceBuffer && count <= _instanceCapacity)
            return;

        _gl.BindVertexArray(_vao);

        if (!_hasInstanceBuffer)
        {
            _instanceVbo = _gl.GenBuffer();
            _hasInstanceBuffer = true;
        }

        _instanceCapacity = Math.Max(count, Math.Max(_instanceCapacity * 2, 64));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        _gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(_instanceCapacity * sizeof(InstanceData)),
            null,
            BufferUsageARB.DynamicDraw
        );

        var stride = (uint)sizeof(InstanceData);
        for (uint col = 0; col < 4; col++)
        {
            var location = InstanceModelLocation + col;
            _gl.VertexAttribPointer(
                location,
                4,
                VertexAttribPointerType.Float,
                false,
                stride,
                (void*)(col * sizeof(Vector4))
            );
            _gl.EnableVertexAttribArray(location);
            _gl.VertexAttribDivisor(location, 1);
        }

        var mat4Size = (uint)sizeof(Matrix4x4);
        for (uint col = 0; col < 3; col++)
        {
            var location = InstanceNormalMatrixLocation + col;
            _gl.VertexAttribPointer(
                location,
                3,
                VertexAttribPointerType.Float,
                false,
                stride,
                (void*)(mat4Size + col * (3 * sizeof(float)))
            );
            _gl.EnableVertexAttribArray(location);
            _gl.VertexAttribDivisor(location, 1);
        }

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _vbo.Dispose();
        _ebo.Dispose();
        if (_hasInstanceBuffer)
            _gl.DeleteBuffer(_instanceVbo);
        if (_hasPaletteOffsetBuffer)
            _gl.DeleteBuffer(_paletteOffsetVbo);
    }
}

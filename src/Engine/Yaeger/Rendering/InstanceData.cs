using System.Numerics;
using System.Runtime.InteropServices;

namespace Yaeger.Rendering;

/// <summary>
/// Per-instance data streamed into a <see cref="GpuMesh"/>'s instance buffer: a model matrix plus the
/// pre-computed normal matrix (the inverse-transpose of the model's upper 3x3), matching exactly what
/// <see cref="Renderer3D"/>'s non-instanced path uploads as the <c>uModel</c>/<c>uNormalMatrix</c>
/// uniforms — so instanced and non-instanced draws of the same transform shade identically.
///
/// Field layout (and therefore struct size/alignment) is load-bearing: <see cref="GpuMesh"/> points
/// vertex attributes directly at the byte offsets of <see cref="Model"/> and the N* fields, so this
/// must stay a tightly packed sequence of floats with <see cref="Model"/> first.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct InstanceData
{
    public readonly Matrix4x4 Model;

    // The normal matrix (row-major top-left 3x3 of Matrix4x4.Transpose(Matrix4x4.Invert(Model))),
    // named to match Shader.SetUniformMatrix3's packing order: column 0 = (N11, N12, N13), etc.
    public readonly float N11,
        N12,
        N13;
    public readonly float N21,
        N22,
        N23;
    public readonly float N31,
        N32,
        N33;

    /// <param name="model">The instance's world-space model matrix.</param>
    /// <param name="normalMatrixSource">
    /// <c>Matrix4x4.Transpose(invModel)</c> (or <see cref="Matrix4x4.Identity"/> when <paramref name="model"/>
    /// isn't invertible) — same value <see cref="Renderer3D"/> passes to <c>SetUniformMatrix3</c> for a
    /// non-instanced draw of this transform.
    /// </param>
    public InstanceData(Matrix4x4 model, Matrix4x4 normalMatrixSource)
    {
        Model = model;
        N11 = normalMatrixSource.M11;
        N12 = normalMatrixSource.M12;
        N13 = normalMatrixSource.M13;
        N21 = normalMatrixSource.M21;
        N22 = normalMatrixSource.M22;
        N23 = normalMatrixSource.M23;
        N31 = normalMatrixSource.M31;
        N32 = normalMatrixSource.M32;
        N33 = normalMatrixSource.M33;
    }

    /// <summary>
    /// Model-only instance data for the shadow depth pre-pass, which doesn't shade fragments and so
    /// never reads the normal-matrix attribute; those fields are left zeroed.
    /// </summary>
    public InstanceData(Matrix4x4 model)
        : this(model, default) { }
}

using System.Numerics;
using System.Runtime.InteropServices;

namespace Yaeger.Rendering;

/// <summary>
/// Per-instance data streamed into <see cref="Renderer3D"/>'s particle billboard instance buffer:
/// world-space centre, billboard size (X = along the velocity-stretch/rotation axis, Y =
/// perpendicular), rotation (radians, applied in the camera's right/up plane — see
/// <see cref="BillboardMath"/>), and colour.
///
/// Field layout (and therefore struct size/alignment) is load-bearing: <see cref="Renderer3D"/>
/// points vertex attributes directly at the byte offsets of these fields, so this must stay a
/// tightly packed sequence of floats in this order.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct ParticleInstanceData(
    Vector3 position,
    Vector2 size,
    float rotation,
    Vector4 color
)
{
    public readonly Vector3 Position = position;
    public readonly Vector2 Size = size;
    public readonly float Rotation = rotation;
    public readonly Vector4 Color = color;
}

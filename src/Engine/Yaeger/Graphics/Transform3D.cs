using System.Numerics;

namespace Yaeger.Graphics;

public record struct Transform3D(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public Matrix4x4 ModelMatrix =>
        Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateFromQuaternion(Rotation)
        * Matrix4x4.CreateTranslation(Position);

    public static Transform3D Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);
}

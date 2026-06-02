using System.Numerics;

namespace Yaeger.Graphics;

public struct Transform3D(Vector3 position, Quaternion rotation, Vector3 scale)
{
    public Vector3 Position = position;
    public Quaternion Rotation = rotation;
    public Vector3 Scale = scale;

    public Matrix4x4 ModelMatrix =>
        Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateFromQuaternion(Rotation)
        * Matrix4x4.CreateTranslation(Position);

    public static Transform3D Identity =>
        new(Vector3.Zero, Quaternion.Identity, Vector3.One);
}

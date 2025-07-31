using System.Numerics;

namespace Yaeger.Graphics;

public struct Transform2D(Vector2 position, float rotation = 0.0f, Vector2? scale = null)
{
    public Vector2 Position = position;
    public float Rotation = rotation;
    public Vector2 Scale = scale ?? Vector2.One;
    
    public Matrix4x4 TransformMatrix => 
        Matrix4x4.CreateScale(new Vector3(Scale, 1)) *
        Matrix4x4.CreateRotationZ(Rotation) *
        Matrix4x4.CreateTranslation(new Vector3(Position, 0));
}

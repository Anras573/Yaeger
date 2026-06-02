using System.Numerics;

namespace Yaeger.Rendering;

public readonly struct Vertex3D
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TexCoord;

    public Vertex3D(Vector3 position, Vector3 normal, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
    }
}

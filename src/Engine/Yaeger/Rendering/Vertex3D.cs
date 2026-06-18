using System.Numerics;

namespace Yaeger.Rendering;

public readonly struct Vertex3D
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TexCoord;
    public readonly Vector3 Tangent;

    /// <summary>Up to four skeleton bone indices influencing this vertex (stored as floats for the GPU).</summary>
    public readonly Vector4 BoneIndices;

    /// <summary>Blend weights for <see cref="BoneIndices"/>; all zero for static (non-skinned) meshes.</summary>
    public readonly Vector4 BoneWeights;

    public Vertex3D(Vector3 position, Vector3 normal, Vector2 texCoord, Vector3 tangent = default)
        : this(position, normal, texCoord, tangent, Vector4.Zero, Vector4.Zero) { }

    public Vertex3D(
        Vector3 position,
        Vector3 normal,
        Vector2 texCoord,
        Vector3 tangent,
        Vector4 boneIndices,
        Vector4 boneWeights
    )
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
        Tangent = tangent;
        BoneIndices = boneIndices;
        BoneWeights = boneWeights;
    }
}

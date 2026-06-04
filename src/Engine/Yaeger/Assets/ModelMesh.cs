using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Assets;

public record ModelMesh(
    string Name,
    MeshData Mesh,
    ModelMaterial Material,
    Transform3D Transform
);

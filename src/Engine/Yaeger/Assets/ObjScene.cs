namespace Yaeger.Assets;

public record ObjScene(
    IReadOnlyList<ObjMesh> Meshes,
    IReadOnlyDictionary<string, MtlMaterial> Materials
);

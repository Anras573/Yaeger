using Yaeger.Graphics;

namespace Yaeger.Assets;

/// <summary>
/// The result of loading a model file. <see cref="Skeleton"/> and <see cref="Animations"/> are
/// populated only for skinned models (e.g. glTF with a skin); they are <c>null</c>/empty for static
/// meshes such as OBJ, leaving existing static-mesh consumers unaffected.
/// </summary>
public record ModelScene(
    IReadOnlyList<ModelMesh> Meshes,
    Skeleton? Skeleton = null,
    IReadOnlyList<AnimationClip>? Animations = null
)
{
    /// <summary>Animation clips extracted from the model; never <c>null</c>.</summary>
    public IReadOnlyList<AnimationClip> Animations { get; init; } = Animations ?? [];
}

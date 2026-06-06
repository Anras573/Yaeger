using Yaeger.Rendering;

namespace Yaeger.Graphics;

/// <summary>
/// ECS component that marks an entity as a skybox.
/// Attach this to any entity and supply a <see cref="SkyboxRenderer"/> to
/// <see cref="Yaeger.Systems.MeshRenderSystem"/> for automatic rendering.
/// </summary>
public readonly record struct Skybox(CubemapTexture Cubemap);

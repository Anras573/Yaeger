namespace Yaeger.Graphics;

/// <summary>
/// ECS component that holds an opaque integer key into a cubemap registry.
/// Attach to an entity alongside a <see cref="CubemapRegistry"/> and
/// <see cref="SkyboxRenderer"/> wired into <see cref="Yaeger.Systems.MeshRenderSystem"/>
/// for automatic skybox rendering.
/// </summary>
public readonly record struct Skybox(int Id);

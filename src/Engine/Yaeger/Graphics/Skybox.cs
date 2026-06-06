namespace Yaeger.Graphics;

/// <summary>
/// ECS component that holds an opaque integer key into a cubemap registry.
/// Attach to an entity; wire a <c>CubemapRegistry</c> and <c>SkyboxRenderer</c>
/// into <c>MeshRenderSystem</c> for automatic skybox rendering.
/// </summary>
public readonly record struct Skybox(int Id);

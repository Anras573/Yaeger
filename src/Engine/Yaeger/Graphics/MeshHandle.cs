namespace Yaeger.Graphics;

/// <summary>
/// ECS component that holds an opaque integer key into a mesh registry.
/// Attach to an entity alongside <see cref="Transform3D"/> and <see cref="Material3D"/>
/// to participate in 3D rendering.
/// </summary>
public readonly record struct MeshHandle(int Id);

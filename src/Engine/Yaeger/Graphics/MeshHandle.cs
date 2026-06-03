namespace Yaeger.Graphics;

/// <summary>
/// ECS component that references a mesh registered in <see cref="Yaeger.Rendering.GpuMeshRegistry"/>.
/// </summary>
public readonly record struct MeshHandle(int Id);

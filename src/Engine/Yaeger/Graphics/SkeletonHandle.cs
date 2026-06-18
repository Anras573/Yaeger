namespace Yaeger.Graphics;

/// <summary>
/// ECS component that holds an opaque integer key into a <see cref="SkeletonRegistry"/>.
/// Attach to a skinned mesh entity alongside <see cref="AnimationPlayer"/> (and the usual
/// <see cref="MeshHandle"/>/<see cref="Transform3D"/>/<see cref="Material3D"/>) to drive
/// skeletal animation.
/// </summary>
public readonly record struct SkeletonHandle(int Id);

using System.Numerics;
using Yaeger.ECS;

namespace Yaeger.Physics;

/// <summary>
/// The result of a hit against <see cref="WorldRaycastExtensions.Raycast"/> or
/// <see cref="WorldRaycastExtensions.RaycastAll"/>: which entity was hit, how far along the ray
/// (in the ray's own units — finite since <see cref="Ray3D.Direction"/> is unit-length), the
/// world-space hit point, and the outward-facing surface normal at that point.
/// </summary>
public readonly record struct RaycastHit(
    Entity Entity,
    float Distance,
    Vector3 Point,
    Vector3 Normal
);

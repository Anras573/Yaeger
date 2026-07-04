using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Systems;

namespace FlightHelmet;

/// <summary>
/// Keeps a <see cref="Camera3D"/> orbiting around a fixed target point. The camera slowly
/// circles the target on its own; dragging with the left mouse button takes over the orbit
/// angles, and the scroll wheel zooms in and out.
/// </summary>
internal sealed class OrbitCameraSystem(
    World world,
    Entity cameraEntity,
    Vector3 target,
    float radius
) : IUpdateSystem
{
    private const float AutoOrbitSpeed = 0.35f; // radians per second
    private const float DragSensitivity = 0.005f; // radians per pixel
    private const float ZoomSensitivity = 0.1f; // fraction of radius per scroll notch

    // Keep the camera from flipping over the poles or diving under the floor.
    private const float MinPitch = -0.15f;
    private const float MaxPitch = 1.35f;

    private readonly float _minRadius = radius * 0.35f;
    private readonly float _maxRadius = radius * 3f;

    private float _yaw;
    private float _pitch = 0.35f;
    private float _radius = radius;
    private bool _autoOrbit = true;

    /// <summary>Pauses or resumes the automatic orbit (manual drag still works while paused).</summary>
    public void ToggleAutoOrbit() => _autoOrbit = !_autoOrbit;

    public void Update(float deltaTime)
    {
        if (!world.TryGetComponent<Camera3D>(cameraEntity, out var camera))
            return;

        if (Mouse.IsButtonPressed(MouseButton.Left))
        {
            var delta = Mouse.PositionDelta;
            _yaw -= delta.X * DragSensitivity;
            _pitch = Math.Clamp(_pitch + delta.Y * DragSensitivity, MinPitch, MaxPitch);
        }
        else if (_autoOrbit)
        {
            _yaw += AutoOrbitSpeed * deltaTime;
        }

        var scroll = Mouse.ScrollDelta;
        if (scroll != 0f)
            _radius = Math.Clamp(_radius * (1f - scroll * ZoomSensitivity), _minRadius, _maxRadius);

        var cosPitch = MathF.Cos(_pitch);
        var offset =
            new Vector3(cosPitch * MathF.Sin(_yaw), MathF.Sin(_pitch), cosPitch * MathF.Cos(_yaw))
            * _radius;

        world.AddComponent(
            cameraEntity,
            camera with
            {
                Position = target + offset,
                Target = target,
                Up = Vector3.UnitY,
            }
        );
    }
}

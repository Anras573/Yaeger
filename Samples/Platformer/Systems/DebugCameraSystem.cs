using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;

namespace Platformer.Systems;

/// <summary>
/// A debug fly-cam toggled by <see cref="Toggle"/> (bound to <c>C</c> in <c>Program.cs</c>),
/// demonstrating the same manual <see cref="Camera2D"/> API as <c>Samples/CameraDemo</c> — pan,
/// zoom, and rotate — inside the flagship sample instead of a standalone demo. While active it
/// replaces <c>CameraFollowSystem</c> for the tagged camera entity; the caller is responsible for
/// not also running <c>CameraFollowSystem.Update</c> during that time (see <c>Program.cs</c>'s
/// use of <see cref="IsActive"/> to pick which one runs each frame).
/// </summary>
public sealed class DebugCameraSystem(World world, Entity cameraEntity)
{
    private const float PanSpeed = 3f;
    private const float ZoomSpeed = 1.5f;
    private const float RotationSpeed = 1.5f;

    private Camera2D _savedCamera;

    public bool IsActive { get; private set; }

    /// <summary>Turns the free-cam on (saving the current <see cref="Camera2D"/> to restore later) or off.</summary>
    public void Toggle()
    {
        if (IsActive)
        {
            world.AddComponent(cameraEntity, _savedCamera);
            IsActive = false;
        }
        else
        {
            _savedCamera = world.GetComponent<Camera2D>(cameraEntity);
            IsActive = true;
        }
    }

    public void Update(float deltaTime)
    {
        if (!IsActive)
            return;

        var camera = world.GetComponent<Camera2D>(cameraEntity);

        var input = Vector2.Zero;
        if (Keyboard.IsKeyPressed(Keys.W))
            input.Y += 1f;
        if (Keyboard.IsKeyPressed(Keys.S))
            input.Y -= 1f;
        if (Keyboard.IsKeyPressed(Keys.A))
            input.X -= 1f;
        if (Keyboard.IsKeyPressed(Keys.D))
            input.X += 1f;

        if (input != Vector2.Zero)
            camera.Position += Vector2.Normalize(input) * PanSpeed * deltaTime / camera.Zoom;

        if (Keyboard.IsKeyPressed(Keys.E))
            camera.Zoom *= MathF.Pow(ZoomSpeed, deltaTime);
        if (Keyboard.IsKeyPressed(Keys.Q))
            camera.Zoom /= MathF.Pow(ZoomSpeed, deltaTime);

        if (Keyboard.IsKeyPressed(Keys.Right))
            camera.Rotation += RotationSpeed * deltaTime;
        if (Keyboard.IsKeyPressed(Keys.Left))
            camera.Rotation -= RotationSpeed * deltaTime;

        world.AddComponent(cameraEntity, camera);
    }
}

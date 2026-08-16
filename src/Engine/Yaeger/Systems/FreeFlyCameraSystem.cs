using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;

namespace Yaeger.Systems;

/// <summary>
/// WASD + right-mouse-drag fly camera: hold the right mouse button and move the mouse to look
/// around, WASD to move forward/back/strafe, E/Q to rise/fall. Writes directly to
/// <see cref="Camera3D.Position"/>/<see cref="Camera3D.Target"/> — the free-fly camera has no
/// parent, so it never needs a <see cref="Transform3D"/>.
/// </summary>
/// <remarks>
/// Promoted from the three near-identical <c>FreeFlySystem</c> copies every 3D sample used to
/// carry (<c>Samples/SkinnedMeshDemo</c>, <c>Samples/Sponza</c>, <c>Samples/CornellBox</c>).
/// Needs <see cref="Keyboard"/>/<see cref="Mouse"/>, so — like <see cref="CameraFollowSystem"/> —
/// it lives in the native <c>Yaeger</c> assembly, not <c>Yaeger.Core</c>.
/// </remarks>
public sealed class FreeFlyCameraSystem(
    World world,
    Entity cameraEntity,
    float moveSpeed = 10f,
    float lookSensitivity = 0.003f
) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        if (!world.TryGetComponent<Camera3D>(cameraEntity, out var camera))
            return;

        if (Mouse.IsButtonPressed(MouseButton.Right))
        {
            var delta = Mouse.PositionDelta;
            if (delta.LengthSquared() > 0f)
            {
                var yaw = -delta.X * lookSensitivity;
                var pitch = -delta.Y * lookSensitivity;

                var fwd = Vector3.Normalize(camera.Target - camera.Position);

                // Yaw around world Y axis.
                fwd = Vector3.Normalize(
                    Vector3.TransformNormal(fwd, Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yaw))
                );

                // Recompute the right axis after yaw so pitch is applied to the post-yaw
                // orientation.
                var r = Vector3.Normalize(Vector3.Cross(fwd, camera.Up));

                // Pitch around the local right axis; reject if the result points nearly straight
                // up/down (gimbal guard — matches Camera3D.ViewMatrix's own near-parallel check).
                var pitched = Vector3.Normalize(
                    Vector3.TransformNormal(fwd, Matrix4x4.CreateFromAxisAngle(r, pitch))
                );
                if (MathF.Abs(pitched.Y) < 0.999f)
                    fwd = pitched;

                camera = camera with { Target = camera.Position + fwd };
            }
        }

        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));

        var move = Vector3.Zero;
        if (Keyboard.IsKeyPressed(Keys.W))
            move += forward;
        if (Keyboard.IsKeyPressed(Keys.S))
            move -= forward;
        if (Keyboard.IsKeyPressed(Keys.A))
            move -= right;
        if (Keyboard.IsKeyPressed(Keys.D))
            move += right;
        if (Keyboard.IsKeyPressed(Keys.E))
            move += Vector3.UnitY;
        if (Keyboard.IsKeyPressed(Keys.Q))
            move -= Vector3.UnitY;

        if (move != Vector3.Zero)
        {
            var displacement = Vector3.Normalize(move) * moveSpeed * deltaTime;
            camera = camera with
            {
                Position = camera.Position + displacement,
                Target = camera.Target + displacement,
            };
        }

        world.AddComponent(cameraEntity, camera);
    }
}

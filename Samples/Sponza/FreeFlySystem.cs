using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Systems;

namespace Sponza;

internal sealed class FreeFlySystem(World world, Entity cameraEntity) : IUpdateSystem
{
    private const float MoveSpeed = 10f;
    private const float LookSensitivity = 0.003f;

    public void Update(float deltaTime)
    {
        if (!world.TryGetComponent<Camera3D>(cameraEntity, out var camera))
            return;

        if (Mouse.IsButtonPressed(MouseButton.Right))
        {
            var delta = Mouse.PositionDelta;
            if (delta.LengthSquared() > 0f)
            {
                var yaw = -delta.X * LookSensitivity;
                var pitch = -delta.Y * LookSensitivity;

                var fwd = Vector3.Normalize(camera.Target - camera.Position);

                // Yaw around world Y axis
                fwd = Vector3.Normalize(
                    Vector3.TransformNormal(fwd, Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yaw))
                );

                // Recompute right axis after yaw so pitch is applied to post-yaw orientation
                var r = Vector3.Normalize(Vector3.Cross(fwd, camera.Up));

                // Pitch around local right axis; reject if result points nearly straight up/down
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
            var displacement = Vector3.Normalize(move) * MoveSpeed * deltaTime;
            camera = camera with
            {
                Position = camera.Position + displacement,
                Target = camera.Target + displacement,
            };
        }

        world.AddComponent(cameraEntity, camera);
    }
}

using System.Numerics;

namespace Yaeger.Engine.Renderer
{
    public class Camera
    {
        public Matrix4x4 Projection { get; private set; }
        public Matrix4x4 View { get; private set; }
        public Matrix4x4 ViewProjection { get; private set; }

        private Vector3 _position = Vector3.Zero;
        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                RecalculateViewMatrix();
            }
        }

        private float _rotation;
        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                RecalculateViewMatrix();
            }
        }

        public Camera(Vector4 projection)
        {
            Projection = Matrix4x4.CreateOrthographicOffCenter(projection.W, projection.X, projection.Y, projection.Z, 1.0f, 1.0f);
            View = Matrix4x4.Identity;

            ViewProjection = Projection * View;
        }

        private void RecalculateViewMatrix()
        {
            Vector3 lookDir = GetLookDir();
            var lookDirection = lookDir;
            View = Matrix4x4.CreateLookAt(_position, _position + lookDirection, Vector3.UnitY);

            ViewProjection = Projection * View;
        }

        private Vector3 GetLookDir()
        {
            var yaw = -0.3f;
            var pitch = 0.1f;
            Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
            Vector3 lookDir = Vector3.Transform(-Vector3.UnitZ, lookRotation);
            return lookDir;
        }
    }
}

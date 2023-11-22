using System.Numerics;
using Yaeger.Engine.Entity;

namespace Yaeger.Engine.Renderer
{
    public interface IRenderContext
    {
        void DrawCube(Cube cube);
        void SetClearColor(Vector4 color);
        void UpdateCameraBuffer(Camera camera);
    }
}

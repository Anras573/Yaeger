using System.Numerics;
using Yaeger.Engine.Core;
using Yaeger.Engine.Entity;
using Yaeger.Engine.Renderer;

namespace Yaeger.Sandbox
{
    internal class GameScene : Scene
    {
        public override string Name => nameof(GameScene);
        private readonly Cube[] _cubes = { new(), new(new Vector3(1, 0, 0), 0.5f), new(new Vector3(-1, 0, 0), 0.5f) };
        private readonly Camera _camera = new(new Vector4(-36f, 20f, 100f, 1f));

        public override void OnAttach()
        {
            Console.WriteLine("Attached");
        }

        public override void OnDetach()
        {
            Console.WriteLine("Detached");
        }

        public override void OnUpdate(float deltaTime, IRenderContext renderContext)
        {
            _camera.Rotation =+ 180 * deltaTime;

            renderContext.UpdateCameraBuffer(_camera);

            renderContext.SetClearColor(new Vector4(0.3921f, 0.5843f, 0.9294f, 1f));

            foreach (var cube in _cubes)
            {
                renderContext.DrawCube(cube);
            }
        }
    }
}

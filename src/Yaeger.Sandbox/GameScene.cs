using Yaeger.Engine.Core;
using Yaeger.Engine.Renderer;

namespace Yaeger.Sandbox
{
    internal class GameScene : Scene
    {
        public override string Name => nameof(GameScene);

        public override void OnAttach()
        {
            Console.WriteLine("Attached");
        }

        public override void OnDetach()
        {
            Console.WriteLine("Detached");
        }

        public override void OnUpdate(int deltaTime, IRenderContext renderContext)
        {
            renderContext.SetClearColor(new System.Numerics.Vector4(0.3921f, 0.5843f, 0.9294f, 1f));
        }
    }
}

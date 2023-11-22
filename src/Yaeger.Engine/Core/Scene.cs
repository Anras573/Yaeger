using Yaeger.Engine.Renderer;

namespace Yaeger.Engine.Core
{
    public abstract class Scene
    {
        public abstract string Name { get; }
        public abstract void OnAttach();
        public abstract void OnDetach();
        public abstract void OnUpdate(float deltaTime, IRenderContext renderContext);
    }
}

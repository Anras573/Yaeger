using System.Collections;

namespace Yaeger.Engine.Core
{
    internal class SceneManager : IEnumerable<Scene>
    {
        private readonly List<Scene> _scenes = new();

        public void AddScene(Scene scene)
        {
            _scenes.Add(scene);
            scene.OnAttach();
        }

        public void RemoveScene(Scene scene)
        {
            if (_scenes.Remove(scene))
            {
                scene.OnDetach();
            }
        }

        #region IEnumerable

        public IEnumerator<Scene> GetEnumerator()
        {
            return ((IEnumerable<Scene>)_scenes).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_scenes).GetEnumerator();
        }
        #endregion
    }
}

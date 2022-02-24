using Veldrid.Sdl2;

namespace Yaeger.Engine.Core
{
    public sealed class Window
    {
        internal readonly Sdl2Window _innerWindow;

        private Window(Sdl2Window innerWindow)
        {
            _innerWindow = innerWindow;
        }

        public static Window Create(Sdl2Window innerWindow) => new(innerWindow);

        public bool Exists => _innerWindow.Exists;

        public void PumpEvents()
        {
            _innerWindow.PumpEvents();
        }

        public void SetTitle(string title) => _innerWindow.Title = title;
    }
}

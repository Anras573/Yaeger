using System.Diagnostics;
using Veldrid;
using Veldrid.StartupUtilities;
using Yaeger.Engine.Core;
using Yaeger.Engine.Renderer;

namespace Yaeger.Engine
{
    public class Application
    {
        public readonly Window Window;
        private readonly RenderContext _renderContext;

        private readonly SceneManager _sceneManager = new();

        private static Application ApplicationInstance;

        private Application()
        {
            var windowCreateInfo = new WindowCreateInfo
            {
                X = 50,
                Y = 50,
                WindowHeight = 720,
                WindowInitialState = WindowState.Normal,
                WindowTitle = "Yeager Engine",
                WindowWidth = 1280,
            };

            var innerWindow = VeldridStartup.CreateWindow(windowCreateInfo);
            Window = Window.Create(innerWindow);

            var graphicsDevice = VeldridStartup.CreateGraphicsDevice(innerWindow, VeldridStartup.GetPlatformDefaultBackend());
            _renderContext = RenderContext.Create(graphicsDevice);
        }

        public static Application Instance => ApplicationInstance ??= new();

        public void Run()
        {
            Stopwatch sw = Stopwatch.StartNew();
            var lastTime = sw.Elapsed;
            while (Window.Exists)
            {
                var current = sw.Elapsed;
                var deltaTime = current - lastTime;

                Window.PumpEvents();

                _renderContext.Begin();

                foreach (var scene in _sceneManager)
                {
                    scene.OnUpdate(deltaTime.Milliseconds, _renderContext);
                }

                _renderContext.End();

                lastTime = current;
            }
        }

        public void AddScene(Scene scene) => _sceneManager.AddScene(scene);
        public string Title { get => Window.Title; set => Window.Title = value; }
    }
}

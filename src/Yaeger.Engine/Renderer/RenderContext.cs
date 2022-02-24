using System.Numerics;
using Veldrid;

namespace Yaeger.Engine.Renderer
{
    public sealed class RenderContext : IRenderContext
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly CommandList _commandList;

        private RenderContext(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
        }

        internal static RenderContext Create(GraphicsDevice graphicsDevice) => new(graphicsDevice);

        internal void Begin()
        {
            _commandList.Begin();
            _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
        }

        internal void End()
        {
            _commandList.End();
            _graphicsDevice.SubmitCommands(_commandList);
            _graphicsDevice.SwapBuffers();
        }

        public void SetClearColor(Vector4 color)
        {
            var rgba = new RgbaFloat(color);
            _commandList.ClearColorTarget(0, rgba);
        }
    }
}

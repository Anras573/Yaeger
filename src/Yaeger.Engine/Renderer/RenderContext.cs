using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;
using Yaeger.Engine.Entity;

namespace Yaeger.Engine.Renderer
{
    public sealed class RenderContext : IRenderContext
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly CommandList _commandList;
        private readonly ResourceFactory _resourceFactory;
        private readonly Pipeline _pipeline;
        private readonly DeviceBuffer _cameraProjViewBuffer;
        private const string VertexCode = @"
#version 450
layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 fsin_Color;
void main()
{
    gl_Position = vec4(Position, 1);
    fsin_Color = Color;
}";

        private const string FragmentCode = @"
#version 450
layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;
void main()
{
    fsout_Color = fsin_Color;
}";

        private RenderContext(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _resourceFactory = _graphicsDevice.ResourceFactory;
            _commandList = _resourceFactory.CreateCommandList();

            VertexLayoutDescription vertexLayout = new(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
                );

            ShaderDescription vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex,
                Encoding.UTF8.GetBytes(VertexCode),
                "main");
            ShaderDescription fragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment,
                Encoding.UTF8.GetBytes(FragmentCode),
                "main");

            var shaders = _resourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

            var pipelineDescription = new GraphicsPipelineDescription
            (
                blendState: BlendStateDescription.SingleOverrideBlend,
                depthStencilStateDescription: DepthStencilStateDescription.DepthOnlyLessEqual,
                rasterizerState: RasterizerStateDescription.Default,
                primitiveTopology: PrimitiveTopology.TriangleList,
                shaderSet: new ShaderSetDescription
                (
                    vertexLayouts: new[] { vertexLayout },
                    shaders: shaders
                ),
                resourceLayouts: Array.Empty<ResourceLayout>(),
                outputs: _graphicsDevice.MainSwapchain.Framebuffer.OutputDescription
            );

            _pipeline = _resourceFactory.CreateGraphicsPipeline(pipelineDescription);

           _cameraProjViewBuffer = _resourceFactory.CreateBuffer(new BufferDescription((4 * 4 * sizeof(float) * 2), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
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
            _graphicsDevice.WaitForIdle();
            _graphicsDevice.SwapBuffers();
        }

        public void SetClearColor(Vector4 color)
        {
            var rgba = new RgbaFloat(color);
            _commandList.ClearColorTarget(0, rgba);
        }

        public void DrawCube(Cube cube)
        {
            _commandList.SetPipeline(_pipeline);

            if (!cube.IsBuffered)
            {
                cube.Buffer(_graphicsDevice.ResourceFactory, _graphicsDevice);
            }

            _commandList.SetVertexBuffer(0, cube.VertexBuffer);
            _commandList.SetIndexBuffer(cube.IndexBuffer, IndexFormat.UInt16);
            _commandList.DrawIndexed((uint)cube.IndexCount, 1, 0, 0, 0);
        }

        public void UpdateCameraBuffer(Camera camera)
        {
            _commandList.UpdateBuffer(_cameraProjViewBuffer, 0, (camera.View, camera.Projection));
        }
    }
}

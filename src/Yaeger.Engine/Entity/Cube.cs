using System.Numerics;
using Veldrid;

namespace Yaeger.Engine.Entity
{
    public class Cube
    {
        internal DeviceBuffer IndexBuffer { get; private set; }
        internal DeviceBuffer VertexBuffer { get; private set; }

        internal VertexPositionColor[] Vertices { get; private set; }

        internal ushort[] Indices =
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23,
        };

        public Cube() : this(Vector3.Zero, 0.5f) { }

        public Cube(Vector3 position, float size)
        {
            var halfSize = size / 2;

            var color = new Vector4(1, 0, 0, 1);
            var color2 = new Vector4(0, 1, 0, 1);
            var color3 = new Vector4(0, 0, 1, 1);

            Vertices = new VertexPositionColor[24]
            {
                // Top
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z - halfSize), color),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z - halfSize), color),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z + halfSize), color),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z + halfSize), color),
                // Bottom                                                             
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z + halfSize), color),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z + halfSize), color),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z - halfSize), color),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z - halfSize), color),
                // Left                                                               
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z - halfSize), color2),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z + halfSize), color2),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z + halfSize), color2),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z - halfSize), color2),
                // Right                                                              
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z + halfSize), color2),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z - halfSize), color2),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z - halfSize), color2),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z + halfSize), color2),
                // Back                                                               
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z - halfSize), color3),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z - halfSize), color3),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z - halfSize), color3),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z - halfSize), color3),
                // Front                                                              
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y + halfSize, position.Z + halfSize), color3),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y + halfSize, position.Z + halfSize), color3),
                new VertexPositionColor(new Vector3(position.X + halfSize, position.Y - halfSize, position.Z + halfSize), color3),
                new VertexPositionColor(new Vector3(position.X - halfSize, position.Y - halfSize, position.Z + halfSize), color3),
            };
        }

        internal bool IsBuffered { get; private set; }

        public int IndexCount { get; private set; }

        internal void Buffer(ResourceFactory resourceFactory, GraphicsDevice graphicsDevice)
        {
            //var color = new Vector4(1, 0, 0, 1);
            //var color2 = new Vector4(0, 1, 0, 1);
            //var color3 = new Vector4(0, 0, 1, 1);

            //VertexPositionColor[] vertices =
            //{
            //    // Top
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, -0.5f), color),
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, -0.5f), color),
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, +0.5f), color),
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, +0.5f), color),
            //    // Bottom                                                             
            //    new VertexPositionColor(new Vector3(-0.5f,-0.5f, +0.5f),  color),
            //    new VertexPositionColor(new Vector3(+0.5f,-0.5f, +0.5f),  color),
            //    new VertexPositionColor(new Vector3(+0.5f,-0.5f, -0.5f),  color),
            //    new VertexPositionColor(new Vector3(-0.5f,-0.5f, -0.5f),  color),
            //    // Left                                                               
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, -0.5f), color2),
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, +0.5f), color2),
            //    new VertexPositionColor(new Vector3(-0.5f, -0.5f, +0.5f), color2),
            //    new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), color2),
            //    // Right                                                              
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, +0.5f), color2),
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, -0.5f), color2),
            //    new VertexPositionColor(new Vector3(+0.5f, -0.5f, -0.5f), color2),
            //    new VertexPositionColor(new Vector3(+0.5f, -0.5f, +0.5f), color2),
            //    // Back                                                               
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, -0.5f), color3),
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, -0.5f), color3),
            //    new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), color3),
            //    new VertexPositionColor(new Vector3(+0.5f, -0.5f, -0.5f), color3),
            //    // Front                                                              
            //    new VertexPositionColor(new Vector3(-0.5f, +0.5f, +0.5f), color3),
            //    new VertexPositionColor(new Vector3(+0.5f, +0.5f, +0.5f), color3),
            //    new VertexPositionColor(new Vector3(+0.5f, -0.5f, +0.5f), color3),
            //    new VertexPositionColor(new Vector3(-0.5f, -0.5f, +0.5f), color3),
            //};

            VertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(VertexPositionColor.SizeInBytes * (uint)Vertices.Length, BufferUsage.VertexBuffer));
            graphicsDevice.UpdateBuffer(VertexBuffer, 0, Vertices);

            IndexBuffer = resourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)Indices.Length, BufferUsage.IndexBuffer));
            graphicsDevice.UpdateBuffer(IndexBuffer, 0, Indices);

            IndexCount = Indices.Length;
            IsBuffered = true;
        }
    }

    struct VertexPositionColor
    {
        public const uint SizeInBytes = 28;
        public Vector3 Position;
        public Vector4 Color;

        public VertexPositionColor(Vector3 position, Vector4 color)
        {
            Position = position;
            Color = color;
        }
    }
}

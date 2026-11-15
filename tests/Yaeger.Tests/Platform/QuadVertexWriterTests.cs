using System.Numerics;
using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class QuadVertexWriterTests
{
    [Fact]
    public void Write_ShouldMatchFullMatrixTransformOfUnitQuad()
    {
        var matrix =
            Matrix4x4.CreateScale(2f, 3f, 1f)
            * Matrix4x4.CreateRotationZ(0.7f)
            * Matrix4x4.CreateTranslation(5f, -4f, 0f);
        var uvMin = new Vector2(0.25f, 0.5f);
        var uvMax = new Vector2(0.75f, 1f);
        var color = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
        Vector3[] corners =
        [
            new(.5f, .5f, 0),
            new(.5f, -.5f, 0),
            new(-.5f, -.5f, 0),
            new(-.5f, .5f, 0),
        ];
        Vector2[] uvs =
        [
            new(uvMax.X, uvMax.Y),
            new(uvMax.X, uvMin.Y),
            new(uvMin.X, uvMin.Y),
            new(uvMin.X, uvMax.Y),
        ];

        var buffer = new float[QuadVertexWriter.FloatsPerQuad];
        QuadVertexWriter.FromMatrix(in matrix, out var origin, out var x, out var y);
        QuadVertexWriter.Write(buffer, origin, x, y, uvMin, uvMax, color);

        for (var c = 0; c < 4; c++)
        {
            var expected = Vector3.Transform(corners[c], matrix);
            var o = c * QuadVertexWriter.FloatsPerVertex;
            Assert.Equal(expected.X, buffer[o + 0], 1e-4f);
            Assert.Equal(expected.Y, buffer[o + 1], 1e-4f);
            Assert.Equal(expected.Z, buffer[o + 2], 1e-4f);
            Assert.Equal(uvs[c].X, buffer[o + 3]);
            Assert.Equal(uvs[c].Y, buffer[o + 4]);
            Assert.Equal([color.X, color.Y, color.Z, color.W], buffer[(o + 5)..(o + 9)]);
        }
    }
}

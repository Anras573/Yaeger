namespace Yaeger.Graphics;

using System.Numerics;

public static class Camera2D
{
    public static Matrix4x4 ProjectionMatrix(int width, int height) =>
          Matrix4x4.CreateOrthographicOffCenter(
            0,
            width,
            height,
            0,
            -1,
            1);
}
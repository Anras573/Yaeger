using System.Numerics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class TranslateHandlesTests
{
    [Fact]
    public void For3D_Identity_ReturnsThreeUnitAxisHandles()
    {
        var handles = TranslateHandles.For3D(new Vector3(1, 2, 3), Quaternion.Identity, 2f);

        Assert.Equal(3, handles.Length);

        Assert.Equal(0, handles[0].AxisIndex);
        Assert.Equal(new Vector3(1, 2, 3), handles[0].Origin);
        Assert.Equal(Vector3.UnitX, handles[0].Direction);
        Assert.Equal(new Vector3(3, 2, 3), handles[0].End);

        Assert.Equal(1, handles[1].AxisIndex);
        Assert.Equal(new Vector3(1, 4, 3), handles[1].End);

        Assert.Equal(2, handles[2].AxisIndex);
        Assert.Equal(new Vector3(1, 2, 5), handles[2].End);
    }

    [Fact]
    public void For3D_Rotated90DegreesAboutZ_RotatesHandleDirections()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var handles = TranslateHandles.For3D(Vector3.Zero, rotation, 1f);

        // Local +X maps onto world +Y under a 90° Z rotation.
        Assert.True(handles[0].Direction.Y > 0.99f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void For3D_InvalidLength_ReturnsEmpty(float length)
    {
        Assert.Empty(TranslateHandles.For3D(Vector3.Zero, Quaternion.Identity, length));
    }

    [Fact]
    public void For3D_NonFiniteOrigin_ReturnsEmpty()
    {
        Assert.Empty(TranslateHandles.For3D(new Vector3(float.NaN, 0, 0), Quaternion.Identity, 1f));
    }

    [Fact]
    public void For2D_Identity_ReturnsTwoAxisHandlesInZPlane()
    {
        var handles = TranslateHandles.For2D(new Vector2(2, 3), 0f, 1.5f);

        Assert.Equal(2, handles.Length);
        Assert.Equal(new Vector3(2, 3, 0), handles[0].Origin);
        Assert.Equal(0, handles[0].AxisIndex);
        Assert.Equal(new Vector3(3.5f, 3, 0), handles[0].End);
        Assert.Equal(1, handles[1].AxisIndex);
        Assert.Equal(new Vector3(2, 4.5f, 0), handles[1].End);
        Assert.Equal(0f, handles[0].End.Z);
        Assert.Equal(0f, handles[1].End.Z);
    }

    [Fact]
    public void For2D_Rotated90Degrees_MapsLocalXOntoWorldY()
    {
        var handles = TranslateHandles.For2D(Vector2.Zero, MathF.PI / 2f, 1f);

        Assert.True(handles[0].Direction.Y > 0.99f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0f)]
    public void For2D_InvalidLength_ReturnsEmpty(float length)
    {
        Assert.Empty(TranslateHandles.For2D(Vector2.Zero, 0f, length));
    }

    [Fact]
    public void AxisHandle_End_IsOriginPlusDirectionTimesLength()
    {
        var handle = new AxisHandle(new Vector3(1, 1, 1), Vector3.UnitY, 3f, 1);

        Assert.Equal(new Vector3(1, 4, 1), handle.End);
    }
}

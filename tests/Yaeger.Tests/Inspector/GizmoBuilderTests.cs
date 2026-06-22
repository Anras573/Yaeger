using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class GizmoBuilderTests
{
    [Fact]
    public void AddLine_StoresSegmentVerbatim()
    {
        var builder = new GizmoBuilder();
        var color = new Vector4(0.1f, 0.2f, 0.3f, 1f);

        builder.AddLine(new Vector3(1, 2, 3), new Vector3(4, 5, 6), color);

        var line = Assert.Single(builder.Lines);
        Assert.Equal(new Vector3(1, 2, 3), line.Start);
        Assert.Equal(new Vector3(4, 5, 6), line.End);
        Assert.Equal(color, line.Color);
    }

    [Fact]
    public void Clear_RemovesAllLines()
    {
        var builder = new GizmoBuilder();
        builder.AddLine(Vector3.Zero, Vector3.One, Vector4.One);

        builder.Clear();

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddAxes_Identity_EmitsThreeUnitAxesWithRgbColors()
    {
        var builder = new GizmoBuilder();

        builder.AddAxes(Vector3.Zero, Quaternion.Identity, 2f);

        Assert.Equal(3, builder.Lines.Count);

        var x = builder.Lines[0];
        var y = builder.Lines[1];
        var z = builder.Lines[2];

        Assert.Equal(new Vector3(2, 0, 0), x.End);
        Assert.Equal(new Vector3(0, 2, 0), y.End);
        Assert.Equal(new Vector3(0, 0, 2), z.End);

        // Red dominant on X, green on Y, blue on Z.
        Assert.True(x.Color.X > x.Color.Y && x.Color.X > x.Color.Z);
        Assert.True(y.Color.Y > y.Color.X && y.Color.Y > y.Color.Z);
        Assert.True(z.Color.Z > z.Color.X && z.Color.Z > z.Color.Y);
    }

    [Fact]
    public void AddArrow_EmitsShaftPlusFourHeadLines()
    {
        var builder = new GizmoBuilder();

        builder.AddArrow(Vector3.Zero, new Vector3(0, 0, 5), Vector4.One, 0.5f);

        Assert.Equal(5, builder.Lines.Count);
        var shaft = builder.Lines[0];
        Assert.Equal(Vector3.Zero, shaft.Start);
        Assert.Equal(new Vector3(0, 0, 5), shaft.End);

        // Every arrowhead line starts at the tip.
        for (var i = 1; i < builder.Lines.Count; i++)
            Assert.Equal(new Vector3(0, 0, 5), builder.Lines[i].Start);
    }

    [Fact]
    public void AddArrow_DegenerateLength_EmitsOnlyShaft()
    {
        var builder = new GizmoBuilder();

        builder.AddArrow(Vector3.One, Vector3.One, Vector4.One, 0.5f);

        Assert.Single(builder.Lines);
    }

    [Fact]
    public void AddCircle_EmitsSegmentLinesAllOnRadius()
    {
        var builder = new GizmoBuilder();
        const int segments = 16;
        const float radius = 3f;

        builder.AddCircle(
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitZ,
            radius,
            Vector4.One,
            segments
        );

        Assert.Equal(segments, builder.Lines.Count);
        foreach (var line in builder.Lines)
        {
            Assert.Equal(radius, line.Start.Length(), 3);
            Assert.Equal(radius, line.End.Length(), 3);
            Assert.Equal(0f, line.Start.Y, 5); // stays in the X/Z plane
        }
    }

    [Fact]
    public void AddCircle_NonPositiveRadius_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddCircle(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, 0f, Vector4.One);

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddWireSphere_EmitsThreeOrthogonalCircles()
    {
        var builder = new GizmoBuilder();
        const int segments = 12;

        builder.AddWireSphere(Vector3.Zero, 1f, Vector4.One, segments);

        Assert.Equal(segments * 3, builder.Lines.Count);
        foreach (var line in builder.Lines)
            Assert.Equal(1f, line.Start.Length(), 3);
    }

    [Fact]
    public void AddWireCone_EmitsBaseCirclePlusFourSpokesFromApex()
    {
        var builder = new GizmoBuilder();
        const int segments = 16;
        var apex = Vector3.Zero;

        builder.AddWireCone(
            apex,
            -Vector3.UnitY,
            height: 4f,
            baseRadius: 2f,
            Vector4.One,
            segments
        );

        Assert.Equal(segments + 4, builder.Lines.Count);

        // The last four lines are the spokes, each starting at the apex.
        for (var i = segments; i < builder.Lines.Count; i++)
            Assert.Equal(apex, builder.Lines[i].Start);
    }

    [Fact]
    public void AddTransformedBox_EmitsTwelveEdgesWithinBounds()
    {
        var builder = new GizmoBuilder();
        var box = new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));

        builder.AddTransformedBox(box, Matrix4x4.Identity, Vector4.One);

        Assert.Equal(12, builder.Lines.Count);
        foreach (var line in builder.Lines)
        {
            AssertWithinBounds(line.Start, box);
            AssertWithinBounds(line.End, box);
        }
    }

    [Fact]
    public void AddTransformedBox_AppliesModelTransform()
    {
        var builder = new GizmoBuilder();
        var box = new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var model = Matrix4x4.CreateTranslation(10, 0, 0);

        builder.AddTransformedBox(box, model, Vector4.One);

        // Every translated corner sits in [9, 11] on X.
        foreach (var line in builder.Lines)
        {
            Assert.InRange(line.Start.X, 9f, 11f);
            Assert.InRange(line.End.X, 9f, 11f);
        }
    }

    private static void AssertWithinBounds(Vector3 point, Aabb3D box)
    {
        Assert.InRange(point.X, box.Min.X - 1e-4f, box.Max.X + 1e-4f);
        Assert.InRange(point.Y, box.Min.Y - 1e-4f, box.Max.Y + 1e-4f);
        Assert.InRange(point.Z, box.Min.Z - 1e-4f, box.Max.Z + 1e-4f);
    }
}

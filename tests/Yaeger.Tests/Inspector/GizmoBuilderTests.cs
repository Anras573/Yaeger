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
    public void AddAxes_CustomColors_OverrideDefaults()
    {
        var builder = new GizmoBuilder();
        var x = new Vector4(0.9f, 0.1f, 0.1f, 1f);
        var y = new Vector4(0.1f, 0.9f, 0.1f, 1f);
        var z = new Vector4(0.1f, 0.1f, 0.9f, 1f);

        builder.AddAxes(Vector3.Zero, Quaternion.Identity, 1f, x, y, z);

        Assert.Equal(x, builder.Lines[0].Color);
        Assert.Equal(y, builder.Lines[1].Color);
        Assert.Equal(z, builder.Lines[2].Color);
    }

    [Fact]
    public void AddAxes2D_CustomColors_OverrideDefaults()
    {
        var builder = new GizmoBuilder();
        var x = new Vector4(0.9f, 0.1f, 0.1f, 1f);
        var y = new Vector4(0.1f, 0.9f, 0.1f, 1f);

        builder.AddAxes2D(Vector2.Zero, 0f, 1f, x, y);

        Assert.Equal(x, builder.Lines[0].Color);
        Assert.Equal(y, builder.Lines[1].Color);
    }

    [Fact]
    public void AddAxes_NonFiniteLength_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddAxes(Vector3.Zero, Quaternion.Identity, float.NaN);
        builder.AddAxes(Vector3.Zero, Quaternion.Identity, float.PositiveInfinity);

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddArrow_NonFiniteHeadSize_EmitsOnlyShaft()
    {
        var builder = new GizmoBuilder();

        builder.AddArrow(Vector3.Zero, new Vector3(0, 0, 5), Vector4.One, float.NaN);

        // The finite shaft is kept; the arrowhead computed from the bad head size is skipped.
        var shaft = Assert.Single(builder.Lines);
        Assert.Equal(Vector3.Zero, shaft.Start);
        Assert.Equal(new Vector3(0, 0, 5), shaft.End);
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
    public void AddCircle_NonFiniteRadius_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddCircle(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, float.NaN, Vector4.One);
        builder.AddCircle(
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitZ,
            float.PositiveInfinity,
            Vector4.One
        );

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddArrow_NonFiniteEndpoint_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddArrow(Vector3.Zero, new Vector3(float.NaN, 0, 0), Vector4.One, 0.5f);
        builder.AddArrow(new Vector3(float.PositiveInfinity, 0, 0), Vector3.One, Vector4.One, 0.5f);

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddWireCone_NonFiniteInput_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        // Non-finite direction, then non-finite base radius.
        builder.AddWireCone(
            Vector3.Zero,
            new Vector3(float.NaN, 0, 0),
            height: 4f,
            baseRadius: 2f,
            Vector4.One
        );
        builder.AddWireCone(
            Vector3.Zero,
            -Vector3.UnitY,
            height: 4f,
            baseRadius: float.PositiveInfinity,
            Vector4.One
        );

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

    [Fact]
    public void AddAxes2D_Identity_EmitsRedXAndGreenYInZPlane()
    {
        var builder = new GizmoBuilder();

        builder.AddAxes2D(Vector2.Zero, 0f, 2f);

        Assert.Equal(2, builder.Lines.Count);
        var x = builder.Lines[0];
        var y = builder.Lines[1];

        Assert.Equal(new Vector3(2, 0, 0), x.End);
        Assert.Equal(new Vector3(0, 2, 0), y.End);
        Assert.True(x.Color.X > x.Color.Y && x.Color.X > x.Color.Z); // red dominant
        Assert.True(y.Color.Y > y.Color.X && y.Color.Y > y.Color.Z); // green dominant
        Assert.Equal(0f, x.End.Z);
        Assert.Equal(0f, y.End.Z);
    }

    [Fact]
    public void AddAxes2D_NonFinite_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddAxes2D(new Vector2(float.NaN, 0), 0f, 1f);
        builder.AddAxes2D(Vector2.Zero, float.PositiveInfinity, 1f);

        Assert.Empty(builder.Lines);
    }

    [Fact]
    public void AddRect_EmitsFourClosedEdgesInZPlane()
    {
        var builder = new GizmoBuilder();

        builder.AddRect(new Vector2(1, 2), new Vector2(3, 1), 0f, Vector4.One);

        Assert.Equal(4, builder.Lines.Count);
        // Corners sit at (1 ± 3, 2 ± 1), all in the Z = 0 plane.
        foreach (var line in builder.Lines)
        {
            Assert.Equal(0f, line.Start.Z);
            Assert.Equal(0f, line.End.Z);
            Assert.InRange(line.Start.X, -2f, 4f);
            Assert.InRange(line.Start.Y, 1f, 3f);
        }
        // The edges form a closed loop: each end meets the next start.
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            Assert.Equal(builder.Lines[i].End, builder.Lines[next].Start);
        }
    }

    [Fact]
    public void AddRect_Rotation90_SwapsExtents()
    {
        var builder = new GizmoBuilder();

        // 90° rotation maps the X half-extent (2) onto the Y axis.
        builder.AddRect(Vector2.Zero, new Vector2(2, 1), MathF.PI / 2f, Vector4.One);

        var maxY = builder.Lines.Max(l => MathF.Max(MathF.Abs(l.Start.Y), MathF.Abs(l.End.Y)));
        var maxX = builder.Lines.Max(l => MathF.Max(MathF.Abs(l.Start.X), MathF.Abs(l.End.X)));
        Assert.Equal(2f, maxY, 3);
        Assert.Equal(1f, maxX, 3);
    }

    [Fact]
    public void AddRect_NonFinite_EmitsNothing()
    {
        var builder = new GizmoBuilder();

        builder.AddRect(new Vector2(float.NaN, 0), Vector2.One, 0f, Vector4.One);
        builder.AddRect(Vector2.Zero, new Vector2(float.PositiveInfinity, 1), 0f, Vector4.One);
        builder.AddRect(Vector2.Zero, Vector2.One, float.NaN, Vector4.One);

        Assert.Empty(builder.Lines);
    }

    private static void AssertWithinBounds(Vector3 point, Aabb3D box)
    {
        Assert.InRange(point.X, box.Min.X - 1e-4f, box.Max.X + 1e-4f);
        Assert.InRange(point.Y, box.Min.Y - 1e-4f, box.Max.Y + 1e-4f);
        Assert.InRange(point.Z, box.Min.Z - 1e-4f, box.Max.Z + 1e-4f);
    }
}

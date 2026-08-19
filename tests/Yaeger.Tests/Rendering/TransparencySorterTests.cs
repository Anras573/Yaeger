using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side sorting/partitioning logic — no GL context needed, unlike the rest of Rendering/.
public class TransparencySorterTests
{
    [Theory]
    [InlineData(MaterialBlendMode.Opaque, false)]
    [InlineData(MaterialBlendMode.Cutout, false)]
    [InlineData(MaterialBlendMode.Transparent, true)]
    [InlineData(MaterialBlendMode.Additive, true)]
    public void IsTransparent_ClassifiesByBlendMode(MaterialBlendMode blendMode, bool expected)
    {
        var material = new Material3D { BlendMode = blendMode };

        Assert.Equal(expected, TransparencySorter.IsTransparent(material));
    }

    [Fact]
    public void ViewDepth_IdentityView_EqualsNegativeZ()
    {
        // Camera at the origin looking down -Z (identity view): a point at Z = -5 is 5 units in
        // front of the camera, so its view depth (distance from camera) is 5.
        var depth = TransparencySorter.ViewDepth(new Vector3(0f, 0f, -5f), Matrix4x4.Identity);

        Assert.Equal(5f, depth, precision: 5);
    }

    [Fact]
    public void ViewDepth_FartherPoint_ProducesLargerDepth()
    {
        var near = TransparencySorter.ViewDepth(new Vector3(0f, 0f, -2f), Matrix4x4.Identity);
        var far = TransparencySorter.ViewDepth(new Vector3(0f, 0f, -10f), Matrix4x4.Identity);

        Assert.True(far > near);
    }

    [Fact]
    public void ViewDepth_RespectsCameraPosition()
    {
        // Camera translated to Z = 3 (view matrix moves the world the opposite way, -3): a point
        // at world Z = -5 is now only 2 units in front of the camera instead of 5.
        var view = Matrix4x4.CreateLookAt(
            new Vector3(0f, 0f, 3f),
            new Vector3(0f, 0f, -1f),
            Vector3.UnitY
        );

        var depth = TransparencySorter.ViewDepth(new Vector3(0f, 0f, -5f), view);

        Assert.Equal(8f, depth, precision: 4);
    }

    [Fact]
    public void SortBackToFront_OrdersFarthestFirst()
    {
        var near = new Vector3(0f, 0f, -1f);
        var mid = new Vector3(0f, 0f, -5f);
        var far = new Vector3(0f, 0f, -10f);
        var entries = new List<Vector3> { near, far, mid };

        TransparencySorter.SortBackToFront(entries, Matrix4x4.Identity, p => p);

        Assert.Equal([far, mid, near], entries);
    }

    [Fact]
    public void SortBackToFront_AlreadySorted_StaysUnchanged()
    {
        var far = new Vector3(0f, 0f, -10f);
        var mid = new Vector3(0f, 0f, -5f);
        var near = new Vector3(0f, 0f, -1f);
        var entries = new List<Vector3> { far, mid, near };

        TransparencySorter.SortBackToFront(entries, Matrix4x4.Identity, p => p);

        Assert.Equal([far, mid, near], entries);
    }

    [Fact]
    public void SortBackToFront_EmptyList_NoOp()
    {
        var entries = new List<Vector3>();

        TransparencySorter.SortBackToFront(entries, Matrix4x4.Identity, p => p);

        Assert.Empty(entries);
    }

    [Fact]
    public void SortBackToFront_UsesModelTranslationSelector()
    {
        var nearModel = Matrix4x4.CreateTranslation(0f, 0f, -1f);
        var farModel = Matrix4x4.CreateTranslation(0f, 0f, -10f);
        var entries = new List<Matrix4x4> { nearModel, farModel };

        TransparencySorter.SortBackToFront(entries, Matrix4x4.Identity, m => m.Translation);

        Assert.Equal([farModel, nearModel], entries);
    }
}

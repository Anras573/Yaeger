using System.Numerics;
using Xunit;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SkeletonTests
{
    // A two-bone chain: root at y=2, child a further y=3 above it (world y=5). Inverse bind poses
    // are the inverses of the bind-pose world transforms, so feeding the bind pose back in must
    // yield an identity palette.
    private static Skeleton TwoBoneChain()
    {
        var rootLocal = Matrix4x4.CreateTranslation(0f, 2f, 0f);
        var childLocal = Matrix4x4.CreateTranslation(0f, 3f, 0f);

        var rootWorld = rootLocal;
        var childWorld = childLocal * rootWorld; // world y = 5

        Matrix4x4.Invert(rootWorld, out var invRoot);
        Matrix4x4.Invert(childWorld, out var invChild);

        var bones = new[] { new Bone("root", -1, rootLocal), new Bone("child", 0, childLocal) };
        return new Skeleton(bones, [invRoot, invChild]);
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual, float tol = 1e-4f)
    {
        Assert.True(
            Math.Abs(expected.M11 - actual.M11) < tol
                && Math.Abs(expected.M22 - actual.M22) < tol
                && Math.Abs(expected.M33 - actual.M33) < tol
                && Math.Abs(expected.M44 - actual.M44) < tol
                && Math.Abs(expected.M41 - actual.M41) < tol
                && Math.Abs(expected.M42 - actual.M42) < tol
                && Math.Abs(expected.M43 - actual.M43) < tol,
            $"Expected {expected} but got {actual}"
        );
    }

    [Fact]
    public void ComputeMatrixPalette_BindPose_ShouldBeIdentity()
    {
        var skeleton = TwoBoneChain();
        var locals = new[] { skeleton.Bones[0].LocalTransform, skeleton.Bones[1].LocalTransform };
        var palette = new Matrix4x4[2];

        skeleton.ComputeMatrixPalette(locals, palette);

        AssertMatrixEqual(Matrix4x4.Identity, palette[0]);
        AssertMatrixEqual(Matrix4x4.Identity, palette[1]);
    }

    [Fact]
    public void ComputeMatrixPalette_EmptyLocals_ShouldFallBackToBindPoseAndBeIdentity()
    {
        var skeleton = TwoBoneChain();
        var palette = new Matrix4x4[2];

        // No local overrides → each bone uses its bind-pose LocalTransform.
        skeleton.ComputeMatrixPalette(ReadOnlySpan<Matrix4x4>.Empty, palette);

        AssertMatrixEqual(Matrix4x4.Identity, palette[0]);
        AssertMatrixEqual(Matrix4x4.Identity, palette[1]);
    }

    [Fact]
    public void ComputeMatrixPalette_AnimatedChild_ShouldMoveChildVertices()
    {
        var skeleton = TwoBoneChain();
        // Move the child bone from y=3 (local) to y=10 (local), root unchanged.
        var locals = new[]
        {
            skeleton.Bones[0].LocalTransform,
            Matrix4x4.CreateTranslation(0f, 10f, 0f),
        };
        var palette = new Matrix4x4[2];

        skeleton.ComputeMatrixPalette(locals, palette);

        // A vertex sitting at the child's bind position (world y=5) should follow the bone to y=12
        // (root y=2 + animated child y=10).
        var bindVertex = new Vector3(0f, 5f, 0f);
        var skinned = Vector3.Transform(bindVertex, palette[1]);

        Assert.Equal(0f, skinned.X, 4);
        Assert.Equal(12f, skinned.Y, 4);
        Assert.Equal(0f, skinned.Z, 4);
    }

    [Fact]
    public void ComputeMatrixPalette_PaletteTooSmall_ShouldThrow()
    {
        var skeleton = TwoBoneChain();
        var palette = new Matrix4x4[1];

        Assert.Throws<ArgumentException>(() =>
            skeleton.ComputeMatrixPalette(ReadOnlySpan<Matrix4x4>.Empty, palette)
        );
    }

    [Fact]
    public void ComputeMatrixPalette_FewerInverseBindPosesThanBones_ShouldThrow()
    {
        // Two bones but only one inverse bind pose.
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity), new Bone("child", 0, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var palette = new Matrix4x4[2];

        Assert.Throws<InvalidOperationException>(() =>
            skeleton.ComputeMatrixPalette(ReadOnlySpan<Matrix4x4>.Empty, palette)
        );
    }
}

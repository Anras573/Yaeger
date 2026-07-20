using System.Numerics;
using Xunit;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class AnimationClipTests
{
    private static BoneTrack TranslationTrack(int boneIndex) =>
        new(
            boneIndex,
            [
                new VectorKey(0f, new Vector3(0f, 0f, 0f)),
                new VectorKey(1f, new Vector3(10f, 0f, 0f)),
            ],
            [],
            []
        );

    [Fact]
    public void BoneTrack_Sample_Midpoint_ShouldInterpolatePosition()
    {
        var track = TranslationTrack(0);

        var m = track.Sample(0.5f);

        Assert.Equal(5f, m.Translation.X, 4);
        Assert.Equal(0f, m.Translation.Y, 4);
    }

    [Fact]
    public void BoneTrack_Sample_BeforeFirstKey_ShouldClampToFirst()
    {
        var track = TranslationTrack(0);

        var m = track.Sample(-5f);

        Assert.Equal(0f, m.Translation.X, 4);
    }

    [Fact]
    public void BoneTrack_Sample_AfterLastKey_ShouldClampToLast()
    {
        var track = TranslationTrack(0);

        var m = track.Sample(99f);

        Assert.Equal(10f, m.Translation.X, 4);
    }

    [Fact]
    public void BoneTrack_Sample_EmptyChannels_ShouldYieldIdentity()
    {
        var track = new BoneTrack(0, [], [], []);

        var m = track.Sample(0.5f);

        Assert.Equal(Matrix4x4.Identity, m);
    }

    [Fact]
    public void BoneTrack_Sample_RotationMidpoint_ShouldSlerp()
    {
        var track = new BoneTrack(
            0,
            [],
            [
                new QuaternionKey(0f, Quaternion.Identity),
                new QuaternionKey(1f, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f)),
            ],
            []
        );

        var m = track.Sample(0.5f);

        // Halfway between 0° and 90° about Y is 45°: rotating +Z should land between +Z and +X.
        var rotated = Vector3.Transform(Vector3.UnitZ, m);
        Assert.Equal(MathF.Sqrt(0.5f), rotated.X, 3);
        Assert.Equal(MathF.Sqrt(0.5f), rotated.Z, 3);
    }

    [Fact]
    public void Clip_Sample_ShouldOnlyOverrideTrackedBones()
    {
        var seeded = Matrix4x4.CreateTranslation(1f, 2f, 3f);
        var locals = new[] { seeded, seeded };

        // Only bone 1 has a track.
        var clip = new AnimationClip("walk", 1f, [TranslationTrack(1)]);
        clip.Sample(0.5f, locals);

        // Bone 0 keeps its seeded transform; bone 1 is replaced by the sampled track.
        Assert.Equal(seeded, locals[0]);
        Assert.Equal(5f, locals[1].Translation.X, 4);
    }

    [Fact]
    public void Clip_Sample_ShouldIgnoreOutOfRangeBoneIndex()
    {
        var locals = new[] { Matrix4x4.Identity };
        var clip = new AnimationClip("x", 1f, [TranslationTrack(5)]);

        // Should not throw even though the track references a bone outside the span.
        clip.Sample(0.5f, locals);

        Assert.Equal(Matrix4x4.Identity, locals[0]);
    }

    [Fact]
    public void BoneTrack_SampleTRS_MatchesComposedSampleMatrix()
    {
        var track = new BoneTrack(
            0,
            [new VectorKey(0f, Vector3.Zero), new VectorKey(1f, new Vector3(10f, 0f, 0f))],
            [
                new QuaternionKey(0f, Quaternion.Identity),
                new QuaternionKey(1f, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f)),
            ],
            [new VectorKey(0f, Vector3.One), new VectorKey(1f, new Vector3(2f, 2f, 2f))]
        );

        var (translation, rotation, scale) = track.SampleTRS(0.5f);
        var expected =
            Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(translation);

        Assert.Equal(expected, track.Sample(0.5f));
    }

    [Fact]
    public void Clip_SampleTRS_ShouldOnlyOverrideTrackedBones()
    {
        var translations = new Vector3[2];
        var rotations = new[] { Quaternion.Identity, Quaternion.Identity };
        var scales = new[] { Vector3.One, Vector3.One };

        // Only bone 1 has a track.
        var clip = new AnimationClip("walk", 1f, [TranslationTrack(1)]);
        clip.SampleTRS(0.5f, translations, rotations, scales);

        // Bone 0 keeps its seeded values; bone 1 is replaced by the sampled track.
        Assert.Equal(Vector3.Zero, translations[0]);
        Assert.Equal(5f, translations[1].X, 4);
    }

    [Fact]
    public void Clip_SampleTRS_ShouldIgnoreOutOfRangeBoneIndex()
    {
        var translations = new[] { new Vector3(1f, 2f, 3f) };
        var rotations = new[] { Quaternion.Identity };
        var scales = new[] { Vector3.One };
        var clip = new AnimationClip("x", 1f, [TranslationTrack(5)]);

        // Should not throw even though the track references a bone outside the spans.
        clip.SampleTRS(0.5f, translations, rotations, scales);

        Assert.Equal(new Vector3(1f, 2f, 3f), translations[0]);
    }
}

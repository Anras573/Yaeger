using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class TweenTests
{
    [Fact]
    public void Constructor_ZeroDuration_ShouldThrow()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tween(entity, TweenChannel.Transform2DPosition, Vector4.Zero, Vector4.One, 0f)
        );
    }

    [Fact]
    public void Constructor_NegativeDuration_ShouldThrow()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tween(entity, TweenChannel.Transform2DPosition, Vector4.Zero, Vector4.One, -1f)
        );
    }

    [Fact]
    public void Constructor_NegativeDelay_ShouldThrow()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tween(
                entity,
                TweenChannel.Transform2DPosition,
                Vector4.Zero,
                Vector4.One,
                1f,
                delay: -0.1f
            )
        );
    }

    [Fact]
    public void Constructor_ValidArguments_ShouldStartAtZeroElapsedAndNotFinished()
    {
        var world = new World();
        var entity = world.CreateEntity();

        var tween = new Tween(
            entity,
            TweenChannel.Transform2DPosition,
            Vector4.Zero,
            Vector4.One,
            2f
        );

        Assert.Equal(0f, tween.ElapsedTime);
        Assert.False(tween.IsFinished);
        Assert.Equal(entity, tween.Target);
    }

    [Fact]
    public void Create_WithVector2_ShouldPackIntoXYOfVector4()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var from = new Vector2(1f, 2f);
        var to = new Vector2(3f, 4f);

        var tween = Tween.Create(entity, TweenChannel.Transform2DPosition, from, to, 1f);

        Assert.Equal(new Vector4(1f, 2f, 0f, 0f), tween.From);
        Assert.Equal(new Vector4(3f, 4f, 0f, 0f), tween.To);
    }

    [Fact]
    public void Create_WithVector3_ShouldPackIntoXYZOfVector4()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var from = new Vector3(1f, 2f, 3f);
        var to = new Vector3(4f, 5f, 6f);

        var tween = Tween.Create(entity, TweenChannel.Transform3DPosition, from, to, 1f);

        Assert.Equal(new Vector4(1f, 2f, 3f, 0f), tween.From);
        Assert.Equal(new Vector4(4f, 5f, 6f, 0f), tween.To);
    }

    [Fact]
    public void Create_WithFloat_ShouldPackIntoXOfVector4()
    {
        var world = new World();
        var entity = world.CreateEntity();

        var tween = Tween.Create(entity, TweenChannel.Material3DOpacity, 0.2f, 0.9f, 1f);

        Assert.Equal(0.2f, tween.From.X);
        Assert.Equal(0.9f, tween.To.X);
    }

    [Fact]
    public void Create_WithQuaternion_ShouldPackAllFourComponents()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var from = Quaternion.Identity;
        var to = Quaternion.CreateFromYawPitchRoll(1f, 0f, 0f);

        var tween = Tween.Create(entity, TweenChannel.Transform3DRotation, from, to, 1f);

        Assert.Equal(new Vector4(from.X, from.Y, from.Z, from.W), tween.From);
        Assert.Equal(new Vector4(to.X, to.Y, to.Z, to.W), tween.To);
    }

    [Fact]
    public void Create_WithColor_ShouldPackNormalizedRgba()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var from = Color.Black;
        var to = Color.White;

        var tween = Tween.Create(entity, TweenChannel.PointLightColor, from, to, 1f);

        Assert.Equal(from.ToVector4(), tween.From);
        Assert.Equal(to.ToVector4(), tween.To);
    }
}

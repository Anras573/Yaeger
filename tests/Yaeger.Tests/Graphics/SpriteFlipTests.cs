using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SpriteFlipTests
{
    [Fact]
    public void Sprite_DefaultFlip_IsFalse()
    {
        var sprite = new Sprite("test.png");

        Assert.False(sprite.FlipX);
        Assert.False(sprite.FlipY);
    }

    [Fact]
    public void Sprite_WithFlipX_SetsFlipX()
    {
        var sprite = new Sprite("test.png", flipX: true);

        Assert.True(sprite.FlipX);
        Assert.False(sprite.FlipY);
    }

    [Fact]
    public void Sprite_WithFlipY_SetsFlipY()
    {
        var sprite = new Sprite("test.png", flipY: true);

        Assert.False(sprite.FlipX);
        Assert.True(sprite.FlipY);
    }

    [Fact]
    public void Sprite_WithBothFlips_SetsBoth()
    {
        var sprite = new Sprite("test.png", flipX: true, flipY: true);

        Assert.True(sprite.FlipX);
        Assert.True(sprite.FlipY);
    }

    // ── ApplyFlip UV math ──────────────────────────────────────────────────────

    [Fact]
    public void ApplyFlip_NoFlip_ShouldReturnUnchanged()
    {
        var (min, max) = Sprite.ApplyFlip(Vector2.Zero, Vector2.One, false, false);

        Assert.Equal(Vector2.Zero, min);
        Assert.Equal(Vector2.One, max);
    }

    [Fact]
    public void ApplyFlip_FlipXOnly_ShouldSwapUBoundsOnly()
    {
        var (min, max) = Sprite.ApplyFlip(Vector2.Zero, Vector2.One, true, false);

        Assert.Equal(new Vector2(1, 0), min);
        Assert.Equal(new Vector2(0, 1), max);
    }

    [Fact]
    public void ApplyFlip_FlipYOnly_ShouldSwapVBoundsOnly()
    {
        var (min, max) = Sprite.ApplyFlip(Vector2.Zero, Vector2.One, false, true);

        Assert.Equal(new Vector2(0, 1), min);
        Assert.Equal(new Vector2(1, 0), max);
    }

    [Fact]
    public void ApplyFlip_BothFlips_ShouldSwapBothBounds()
    {
        var (min, max) = Sprite.ApplyFlip(Vector2.Zero, Vector2.One, true, true);

        Assert.Equal(new Vector2(1, 1), min);
        Assert.Equal(new Vector2(0, 0), max);
    }

    [Fact]
    public void ApplyFlip_SubRegion_ShouldFlipWithinItsOwnBounds()
    {
        // A SpriteSheet frame's sub-rectangle, not the whole [0,1] texture.
        var uvMin = new Vector2(0.25f, 0.5f);
        var uvMax = new Vector2(0.5f, 0.75f);

        var (min, max) = Sprite.ApplyFlip(uvMin, uvMax, flipX: true, flipY: false);

        // U bounds swapped, V bounds untouched — mirrors within the frame's own extent.
        Assert.Equal(new Vector2(0.5f, 0.5f), min);
        Assert.Equal(new Vector2(0.25f, 0.75f), max);
    }

    [Fact]
    public void ApplyFlip_DoubleFlip_ShouldReturnToOriginal()
    {
        var uvMin = new Vector2(0.1f, 0.2f);
        var uvMax = new Vector2(0.6f, 0.8f);

        var (onceMin, onceMax) = Sprite.ApplyFlip(uvMin, uvMax, true, true);
        var (twiceMin, twiceMax) = Sprite.ApplyFlip(onceMin, onceMax, true, true);

        Assert.Equal(uvMin, twiceMin);
        Assert.Equal(uvMax, twiceMax);
    }
}

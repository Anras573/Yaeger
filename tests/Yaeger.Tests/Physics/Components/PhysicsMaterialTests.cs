using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class PhysicsMaterialTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var material = new PhysicsMaterial(0.5f, 0.3f);

        Assert.Equal(0.5f, material.Restitution);
        Assert.Equal(0.3f, material.Friction);
    }

    [Fact]
    public void Default_ShouldHaveModerateValues()
    {
        var material = PhysicsMaterial.Default;

        Assert.Equal(0.3f, material.Restitution);
        Assert.Equal(0.4f, material.Friction);
    }

    [Fact]
    public void Bouncy_ShouldHaveFullRestitutionNoFriction()
    {
        var material = PhysicsMaterial.Bouncy;

        Assert.Equal(1.0f, material.Restitution);
        Assert.Equal(0.0f, material.Friction);
    }

    [Fact]
    public void Sticky_ShouldHaveNoRestitutionHighFriction()
    {
        var material = PhysicsMaterial.Sticky;

        Assert.Equal(0.0f, material.Restitution);
        Assert.Equal(1.0f, material.Friction);
    }

    [Fact]
    public void Constructor_WithNegativeRestitution_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsMaterial(-0.1f, 0.5f));
    }

    [Fact]
    public void Constructor_WithRestitutionAboveOne_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsMaterial(1.1f, 0.5f));
    }

    [Fact]
    public void Constructor_WithNegativeFriction_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsMaterial(0.5f, -0.1f));
    }
}

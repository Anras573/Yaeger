using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class RigidBody2DTests
{
    [Fact]
    public void CreateDynamic_ShouldSetMassAndInverseMass()
    {
        var body = RigidBody2D.CreateDynamic(2.0f);

        Assert.Equal(2.0f, body.Mass);
        Assert.Equal(0.5f, body.InverseMass);
        Assert.Equal(BodyType.Dynamic, body.Type);
    }

    [Fact]
    public void CreateDynamic_ShouldDefaultGravityScaleToOne()
    {
        var body = RigidBody2D.CreateDynamic(1.0f);

        Assert.Equal(1.0f, body.GravityScale);
    }

    [Fact]
    public void CreateDynamic_ShouldAllowCustomGravityScale()
    {
        var body = RigidBody2D.CreateDynamic(1.0f, gravityScale: 0.5f);

        Assert.Equal(0.5f, body.GravityScale);
    }

    [Fact]
    public void CreateDynamic_ShouldAllowCustomLinearDrag()
    {
        var body = RigidBody2D.CreateDynamic(1.0f, linearDrag: 0.1f);

        Assert.Equal(0.1f, body.LinearDrag);
    }

    [Fact]
    public void CreateDynamic_WithZeroMass_ShouldHaveZeroInverseMass()
    {
        var body = RigidBody2D.CreateDynamic(0.0f);

        Assert.Equal(0.0f, body.InverseMass);
    }

    [Fact]
    public void CreateStatic_ShouldHaveZeroMassAndInverseMass()
    {
        var body = RigidBody2D.CreateStatic();

        Assert.Equal(0.0f, body.Mass);
        Assert.Equal(0.0f, body.InverseMass);
        Assert.Equal(BodyType.Static, body.Type);
    }

    [Fact]
    public void CreateStatic_ShouldHaveZeroGravityScale()
    {
        var body = RigidBody2D.CreateStatic();

        Assert.Equal(0.0f, body.GravityScale);
    }

    [Fact]
    public void CreateKinematic_ShouldHaveZeroMassAndInverseMass()
    {
        var body = RigidBody2D.CreateKinematic();

        Assert.Equal(0.0f, body.Mass);
        Assert.Equal(0.0f, body.InverseMass);
        Assert.Equal(BodyType.Kinematic, body.Type);
    }

    [Fact]
    public void CreateKinematic_ShouldHaveZeroGravityScale()
    {
        var body = RigidBody2D.CreateKinematic();

        Assert.Equal(0.0f, body.GravityScale);
    }
}

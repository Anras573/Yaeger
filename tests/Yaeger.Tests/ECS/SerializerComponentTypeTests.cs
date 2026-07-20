using Yaeger.ECS.Serializers;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class SerializerComponentTypeTests
{
    [Fact]
    public void BoxCollider2DSerializer_ComponentType_ReturnsBoxCollider2DType()
    {
        var serializer = new BoxCollider2DSerializer();
        Assert.Equal(typeof(BoxCollider2D), serializer.ComponentType);
    }

    [Fact]
    public void CircleCollider2DSerializer_ComponentType_ReturnsCircleCollider2DType()
    {
        var serializer = new CircleCollider2DSerializer();
        Assert.Equal(typeof(CircleCollider2D), serializer.ComponentType);
    }

    [Fact]
    public void Transform2DSerializer_ComponentType_ReturnsTransform2DType()
    {
        var serializer = new Transform2DSerializer();
        Assert.Equal(typeof(Transform2D), serializer.ComponentType);
    }

    [Fact]
    public void SpriteSerializer_ComponentType_ReturnsSpriteType()
    {
        var serializer = new SpriteSerializer();
        Assert.Equal(typeof(Sprite), serializer.ComponentType);
    }

    [Fact]
    public void SpriteSheetSerializer_ComponentType_ReturnsSpriteSheetType()
    {
        var serializer = new SpriteSheetSerializer();
        Assert.Equal(typeof(SpriteSheet), serializer.ComponentType);
    }

    [Fact]
    public void AnimationSerializer_ComponentType_ReturnsAnimationType()
    {
        var serializer = new AnimationSerializer();
        Assert.Equal(typeof(Animation), serializer.ComponentType);
    }

    [Fact]
    public void AnimationStateSerializer_ComponentType_ReturnsAnimationStateType()
    {
        var serializer = new AnimationStateSerializer();
        Assert.Equal(typeof(AnimationState), serializer.ComponentType);
    }

    [Fact]
    public void RenderLayerSerializer_ComponentType_ReturnsRenderLayerType()
    {
        var serializer = new RenderLayerSerializer();
        Assert.Equal(typeof(RenderLayer), serializer.ComponentType);
    }

    [Fact]
    public void Transform3DSerializer_ComponentType_ReturnsTransform3DType()
    {
        var serializer = new Transform3DSerializer();
        Assert.Equal(typeof(Transform3D), serializer.ComponentType);
    }

    [Fact]
    public void Camera3DSerializer_ComponentType_ReturnsCamera3DType()
    {
        var serializer = new Camera3DSerializer();
        Assert.Equal(typeof(Camera3D), serializer.ComponentType);
    }

    [Fact]
    public void Material3DSerializer_ComponentType_ReturnsMaterial3DType()
    {
        var serializer = new Material3DSerializer();
        Assert.Equal(typeof(Material3D), serializer.ComponentType);
    }

    [Fact]
    public void DirectionalLightSerializer_ComponentType_ReturnsDirectionalLightType()
    {
        var serializer = new DirectionalLightSerializer();
        Assert.Equal(typeof(DirectionalLight), serializer.ComponentType);
    }

    [Fact]
    public void PointLightSerializer_ComponentType_ReturnsPointLightType()
    {
        var serializer = new PointLightSerializer();
        Assert.Equal(typeof(PointLight), serializer.ComponentType);
    }

    [Fact]
    public void SpotLightSerializer_ComponentType_ReturnsSpotLightType()
    {
        var serializer = new SpotLightSerializer();
        Assert.Equal(typeof(SpotLight), serializer.ComponentType);
    }

    [Fact]
    public void Camera2DSerializer_ComponentType_ReturnsCamera2DType()
    {
        var serializer = new Camera2DSerializer();
        Assert.Equal(typeof(Camera2D), serializer.ComponentType);
    }

    [Fact]
    public void TextSerializer_ComponentType_ReturnsTextType()
    {
        var serializer = new TextSerializer();
        Assert.Equal(typeof(Text), serializer.ComponentType);
    }

    [Fact]
    public void ParticleEmitterSerializer_ComponentType_ReturnsParticleEmitterType()
    {
        var serializer = new ParticleEmitterSerializer();
        Assert.Equal(typeof(ParticleEmitter), serializer.ComponentType);
    }

    [Fact]
    public void ParallaxLayerSerializer_ComponentType_ReturnsParallaxLayerType()
    {
        var serializer = new ParallaxLayerSerializer();
        Assert.Equal(typeof(ParallaxLayer), serializer.ComponentType);
    }

    [Fact]
    public void RigidBody2DSerializer_ComponentType_ReturnsRigidBody2DType()
    {
        var serializer = new RigidBody2DSerializer();
        Assert.Equal(typeof(RigidBody2D), serializer.ComponentType);
    }

    [Fact]
    public void Velocity2DSerializer_ComponentType_ReturnsVelocity2DType()
    {
        var serializer = new Velocity2DSerializer();
        Assert.Equal(typeof(Velocity2D), serializer.ComponentType);
    }

    [Fact]
    public void PhysicsMaterialSerializer_ComponentType_ReturnsPhysicsMaterialType()
    {
        var serializer = new PhysicsMaterialSerializer();
        Assert.Equal(typeof(PhysicsMaterial), serializer.ComponentType);
    }
}

using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class SerializerComponentTypeTests
{
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
}

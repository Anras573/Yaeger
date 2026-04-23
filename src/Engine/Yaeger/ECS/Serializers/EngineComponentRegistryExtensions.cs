namespace Yaeger.ECS.Serializers;

/// <summary>
/// Extension methods for <see cref="ComponentRegistry"/> that register all engine-provided
/// component serializers in a single call.
/// </summary>
public static class EngineComponentRegistryExtensions
{
    /// <summary>
    /// Registers serializers for all built-in engine component types:
    /// <list type="bullet">
    ///   <item><see cref="Yaeger.Graphics.Sprite"/> – type id <c>"Sprite"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Transform2D"/> – type id <c>"Transform2D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.SpriteSheet"/> – type id <c>"SpriteSheet"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Animation"/> – type id <c>"Animation"</c></item>
    ///   <item><see cref="Yaeger.Graphics.AnimationState"/> – type id <c>"AnimationState"</c></item>
    /// </list>
    /// </summary>
    /// <returns>The same <paramref name="registry"/> for method chaining.</returns>
    public static ComponentRegistry RegisterEngineComponents(this ComponentRegistry registry)
    {
        registry.Register(new SpriteSerializer());
        registry.Register(new Transform2DSerializer());
        registry.Register(new SpriteSheetSerializer());
        registry.Register(new AnimationSerializer());
        registry.Register(new AnimationStateSerializer());
        return registry;
    }
}

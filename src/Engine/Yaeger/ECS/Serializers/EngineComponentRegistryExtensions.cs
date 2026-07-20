namespace Yaeger.ECS.Serializers;

/// <summary>
/// Extension methods for <see cref="ComponentRegistry"/> that register all engine-provided
/// component serializers in a single call.
/// </summary>
public static class EngineComponentRegistryExtensions
{
    /// <summary>
    /// Registers serializers for all built-in engine component types.
    /// <para>2D components:</para>
    /// <list type="bullet">
    ///   <item><see cref="Yaeger.Graphics.Sprite"/> – type id <c>"Sprite"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Transform2D"/> – type id <c>"Transform2D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.SpriteSheet"/> – type id <c>"SpriteSheet"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Animation"/> – type id <c>"Animation"</c></item>
    ///   <item><see cref="Yaeger.Graphics.AnimationState"/> – type id <c>"AnimationState"</c></item>
    ///   <item><see cref="Yaeger.Graphics.AnimationStateMachine"/> – type id <c>"AnimationStateMachine"</c></item>
    ///   <item><see cref="Yaeger.Graphics.RenderLayer"/> – type id <c>"RenderLayer"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Tilemap"/> – type id <c>"Tilemap"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Camera2D"/> – type id <c>"Camera2D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.ParticleEmitter"/> – type id <c>"ParticleEmitter"</c></item>
    ///   <item><see cref="Yaeger.Graphics.ParallaxLayer"/> – type id <c>"ParallaxLayer"</c></item>
    ///   <item><see cref="Yaeger.Physics.Components.BoxCollider2D"/> – type id <c>"BoxCollider2D"</c></item>
    ///   <item><see cref="Yaeger.Physics.Components.CircleCollider2D"/> – type id <c>"CircleCollider2D"</c></item>
    ///   <item><see cref="Yaeger.Physics.Components.RigidBody2D"/> – type id <c>"RigidBody2D"</c></item>
    ///   <item><see cref="Yaeger.Physics.Components.Velocity2D"/> – type id <c>"Velocity2D"</c></item>
    ///   <item><see cref="Yaeger.Physics.Components.PhysicsMaterial"/> – type id <c>"PhysicsMaterial"</c></item>
    /// </list>
    /// <para>3D components:</para>
    /// <list type="bullet">
    ///   <item><see cref="Yaeger.Graphics.Transform3D"/> – type id <c>"Transform3D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Camera3D"/> – type id <c>"Camera3D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.Material3D"/> – type id <c>"Material3D"</c></item>
    ///   <item><see cref="Yaeger.Graphics.DirectionalLight"/> – type id <c>"DirectionalLight"</c></item>
    ///   <item><see cref="Yaeger.Graphics.PointLight"/> – type id <c>"PointLight"</c></item>
    ///   <item><see cref="Yaeger.Graphics.SpotLight"/> – type id <c>"SpotLight"</c></item>
    /// </list>
    /// <para>
    /// <see cref="Yaeger.Graphics.MeshHandle"/> is intentionally not registered: its <c>Id</c> is an
    /// opaque, runtime-assigned key into a <c>GpuMeshRegistry</c> and is not portable across runs,
    /// so it is treated as code-assigned rather than persisted.
    /// </para>
    /// <para>
    /// <see cref="Yaeger.Graphics.Text"/> is also not registered here, for a different reason: it
    /// holds a native <c>Yaeger.Font.Font</c> reference, which has no <c>Yaeger.Core</c> equivalent,
    /// so <see cref="Yaeger.ECS.Serializers.TextSerializer"/> can only compile into the native
    /// runtime. This method lives in the shared <c>ECS/Serializers</c> source (compiled into both
    /// <c>Yaeger.Core</c> and <c>Yaeger</c>), so it can't reference native-only types. Native games
    /// that want <c>Text</c> to round-trip through scenes should call
    /// <see cref="NativeComponentRegistryExtensions.RegisterNativeEngineComponents"/> instead, which
    /// registers everything this method does plus <see cref="Yaeger.ECS.Serializers.TextSerializer"/>.
    /// </para>
    /// </summary>
    /// <returns>The same <paramref name="registry"/> for method chaining.</returns>
    public static ComponentRegistry RegisterEngineComponents(this ComponentRegistry registry)
    {
        // 2D components
        registry.Register(new SpriteSerializer());
        registry.Register(new Transform2DSerializer());
        registry.Register(new SpriteSheetSerializer());
        registry.Register(new AnimationSerializer());
        registry.Register(new AnimationStateSerializer());
        registry.Register(new AnimationStateMachineSerializer());
        registry.Register(new RenderLayerSerializer());
        registry.Register(new TilemapSerializer());
        registry.Register(new Camera2DSerializer());
        registry.Register(new ParticleEmitterSerializer());
        registry.Register(new ParallaxLayerSerializer());
        registry.Register(new BoxCollider2DSerializer());
        registry.Register(new CircleCollider2DSerializer());
        registry.Register(new RigidBody2DSerializer());
        registry.Register(new Velocity2DSerializer());
        registry.Register(new PhysicsMaterialSerializer());

        // 3D components
        registry.Register(new Transform3DSerializer());
        registry.Register(new Camera3DSerializer());
        registry.Register(new Material3DSerializer());
        registry.Register(new DirectionalLightSerializer());
        registry.Register(new PointLightSerializer());
        registry.Register(new SpotLightSerializer());

        return registry;
    }
}

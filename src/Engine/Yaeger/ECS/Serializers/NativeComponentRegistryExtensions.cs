namespace Yaeger.ECS.Serializers;

/// <summary>
/// Extension methods for <see cref="ComponentRegistry"/> that register component serializers
/// only available in the native (Silk.NET) runtime.
/// </summary>
/// <remarks>
/// This file is compiled into <c>Yaeger</c> only, not <c>Yaeger.Core</c> — unlike most of
/// <c>ECS/Serializers</c>, which is shared between both via a linked-file glob (see
/// <c>Yaeger.Core.csproj</c>). It exists because <see cref="TextSerializer"/> references
/// <see cref="Yaeger.Graphics.Text"/>, which holds a native <c>Yaeger.Font.Font</c> reference with
/// no <c>Yaeger.Core</c> equivalent, so it can't be registered from the shared
/// <see cref="EngineComponentRegistryExtensions.RegisterEngineComponents"/>.
/// </remarks>
public static class NativeComponentRegistryExtensions
{
    /// <summary>
    /// Registers everything <see cref="EngineComponentRegistryExtensions.RegisterEngineComponents"/>
    /// does, plus <see cref="TextSerializer"/> (type id <c>"Text"</c>) — the full set of built-in
    /// serializers available to a native game.
    /// </summary>
    /// <returns>The same <paramref name="registry"/> for method chaining.</returns>
    public static ComponentRegistry RegisterNativeEngineComponents(this ComponentRegistry registry)
    {
        registry.RegisterEngineComponents();
        registry.Register(new TextSerializer());

        return registry;
    }
}

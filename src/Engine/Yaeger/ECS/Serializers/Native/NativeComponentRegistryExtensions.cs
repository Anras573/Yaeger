namespace Yaeger.ECS.Serializers;

/// <summary>
/// Extension methods for <see cref="ComponentRegistry"/> that register component serializers
/// only available in the native (Silk.NET) runtime.
/// </summary>
/// <remarks>
/// Lives under <c>ECS/Serializers/Native/</c> rather than alongside the other extension
/// methods: unlike most of <c>ECS/Serializers</c> (shared between <c>Yaeger.Core</c> and
/// <c>Yaeger</c> via a linked-file glob — see <c>Yaeger.Core.csproj</c>), everything under
/// <c>Native/</c> compiles into <c>Yaeger</c> only. This file exists because
/// <see cref="TextSerializer"/> references <see cref="Yaeger.Graphics.Text"/>, which holds a
/// native <c>Yaeger.Font.Font</c> reference with no <c>Yaeger.Core</c> equivalent, so it can't be
/// registered from the shared
/// <see cref="EngineComponentRegistryExtensions.RegisterEngineComponents"/>. Any future
/// native-only component serializer belongs in this folder too — that one glob change is all
/// that's needed on both ends (<c>Yaeger.Core.csproj</c> excludes it, <c>Yaeger.csproj</c>
/// re-includes it), and <c>Yaeger.Core</c>'s <c>InternalsVisibleTo</c> to this assembly means
/// <c>Native/</c> files can still use <c>ComponentJson</c>/<c>ComponentJson2D</c>.
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

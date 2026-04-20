namespace Yaeger.ECS;

/// <summary>
/// Maps component type identifiers to their <see cref="IComponentSerializer"/> implementations,
/// enabling <see cref="PrefabLoader"/> to deserialize JSON prefab files.
/// </summary>
/// <remarks>
/// Register engine-provided serializers with the
/// <c>RegisterEngineComponents()</c> extension method in
/// <c>Yaeger.ECS.Serializers.EngineComponentRegistryExtensions</c>, then add any
/// game-specific serializers with <see cref="Register"/>.
/// </remarks>
public sealed class ComponentRegistry
{
    private readonly Dictionary<string, IComponentSerializer> _serializers = new();

    /// <summary>
    /// Registers a component serializer.
    /// Re-registering the same <see cref="IComponentSerializer.TypeId"/> replaces the
    /// existing entry.
    /// </summary>
    /// <param name="serializer">The serializer to register.</param>
    public void Register(IComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializers[serializer.TypeId] = serializer;
    }

    /// <summary>
    /// Returns a read-only snapshot of all currently registered type identifiers.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTypeIds => _serializers.Keys.ToArray();

    internal bool TryGetSerializer(
        string typeId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IComponentSerializer? serializer
    ) => _serializers.TryGetValue(typeId, out serializer);
}

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

        string? typeId = serializer.TypeId;
        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new ArgumentException(
                "Serializer TypeId cannot be null, empty, or whitespace.",
                nameof(serializer)
            );
        }

        _serializers[typeId] = serializer;
    }

    /// <summary>
    /// Returns a read-only snapshot of all currently registered type identifiers.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTypeIds => _serializers.Keys.ToArray();

    internal bool TryGetSerializer(
        string typeId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IComponentSerializer? serializer
    ) => _serializers.TryGetValue(typeId, out serializer);

    /// <summary>
    /// Parses a single component from a JSON element using registered serializers.
    /// </summary>
    /// <param name="componentEl">The JSON element representing the component.</param>
    /// <param name="createException">Factory function to create loader-specific exceptions (message only).</param>
    /// <param name="createExceptionWithInner">Factory function to create loader-specific exceptions with inner exception.</param>
    /// <param name="context">Optional context prefix for error messages (e.g., "Entity 0, component 1: ").</param>
    /// <returns>An action that applies the component to an entity when called.</returns>
    internal Action<World, Entity> ParseComponent(
        System.Text.Json.JsonElement componentEl,
        Func<string, Exception> createException,
        Func<string, Exception, Exception> createExceptionWithInner,
        string? context = null
    )
    {
        var prefix = context ?? "";

        if (componentEl.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw createException($"{prefix}must be a JSON object.");

        if (!componentEl.TryGetProperty("type", out var typeEl))
            throw createException($"{prefix}must have a 'type' property.");

        if (typeEl.ValueKind != System.Text.Json.JsonValueKind.String)
            throw createException($"{prefix}'type' must be a string.");

        var typeId = typeEl.GetString();
        if (string.IsNullOrWhiteSpace(typeId))
            throw createException($"{prefix}'type' must be a non-empty string.");

        if (!TryGetSerializer(typeId, out var serializer))
        {
            var registered = string.Join(", ", RegisteredTypeIds);
            throw createException(
                $"{prefix}no serializer is registered for component type '{typeId}'. "
                    + $"Registered types: [{registered}]"
            );
        }

        try
        {
            return serializer.Deserialize(componentEl.Clone());
        }
        catch (Exception ex) when (ShouldWrapException(ex, createException))
        {
            throw createExceptionWithInner(
                $"{prefix}failed to deserialize component '{typeId}'.",
                ex
            );
        }
    }

    private static bool ShouldWrapException(Exception ex, Func<string, Exception> createException)
    {
        // Don't wrap if it's already the same exception type as what the factory creates
        var sampleException = createException("test");
        return ex.GetType() != sampleException.GetType();
    }
}

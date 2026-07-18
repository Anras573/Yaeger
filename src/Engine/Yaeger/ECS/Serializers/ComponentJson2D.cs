using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Shared JSON read/write helpers for the 2D physics collider serializers
/// (<see cref="BoxCollider2DSerializer"/>, <see cref="CircleCollider2DSerializer"/>).
/// </summary>
internal static class ComponentJson2D
{
    public static float ReadSingle(JsonElement el, string propertyName)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetSingle(out var value))
            throw new PrefabLoadException($"Property '{propertyName}' must be a number.");

        return value;
    }

    public static Vector2 ReadVector2(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var arr = el.EnumerateArray().ToArray();
            if (arr.Length != 2)
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must be a Vector2 array with exactly two numeric elements."
                );

            return new Vector2(
                ReadSingle(arr[0], $"{propertyName}[0]"),
                ReadSingle(arr[1], $"{propertyName}[1]")
            );
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            if (!el.TryGetProperty("x", out var xEl))
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must contain an 'x' number."
                );

            if (!el.TryGetProperty("y", out var yEl))
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must contain a 'y' number."
                );

            return new Vector2(
                ReadSingle(xEl, $"{propertyName}.x"),
                ReadSingle(yEl, $"{propertyName}.y")
            );
        }

        throw new PrefabLoadException(
            $"Property '{propertyName}' must be a Vector2 represented as [x, y] or {{ \"x\": number, \"y\": number }}."
        );
    }

    public static JsonArray Write(Vector2 v) => new(JsonValue.Create(v.X), JsonValue.Create(v.Y));

    /// <summary>
    /// Reads an optional boolean property, returning <paramref name="defaultValue"/> when absent.
    /// </summary>
    public static bool ReadOptionalBool(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var el))
            return defaultValue;

        if (el.ValueKind != JsonValueKind.True && el.ValueKind != JsonValueKind.False)
            throw new PrefabLoadException($"Property '{propertyName}' must be a boolean.");

        return el.GetBoolean();
    }

    /// <summary>
    /// Reads the shared collision-filtering properties (<c>layer</c>, <c>collidesWith</c>,
    /// <c>isTrigger</c>) common to <see cref="BoxCollider2D"/> and <see cref="CircleCollider2D"/>.
    /// </summary>
    public static (int Layer, uint CollidesWith, bool IsTrigger) ReadCollisionFiltering(
        JsonElement element
    )
    {
        var layer = 0;
        if (element.TryGetProperty("layer", out var layerEl) && !layerEl.TryGetInt32(out layer))
            throw new PrefabLoadException("Property 'layer' must be an integer.");

        var collidesWith = BoxCollider2D.AllLayers;
        if (
            element.TryGetProperty("collidesWith", out var collidesWithEl)
            && !collidesWithEl.TryGetUInt32(out collidesWith)
        )
            throw new PrefabLoadException(
                "Property 'collidesWith' must be a non-negative integer bitmask."
            );

        var isTrigger = ReadOptionalBool(element, "isTrigger", false);

        return (layer, collidesWith, isTrigger);
    }

    /// <summary>
    /// Writes the shared collision-filtering properties, omitting each one when it is at its
    /// default value so unfiltered colliders round-trip to a minimal JSON payload.
    /// </summary>
    public static void WriteCollisionFiltering(
        JsonObject obj,
        int layer,
        uint collidesWith,
        bool isTrigger
    )
    {
        if (layer != 0)
            obj["layer"] = layer;

        if (collidesWith != BoxCollider2D.AllLayers)
            obj["collidesWith"] = collidesWith;

        if (isTrigger)
            obj["isTrigger"] = isTrigger;
    }
}

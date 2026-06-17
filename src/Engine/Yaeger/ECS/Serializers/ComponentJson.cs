using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Shared JSON read/write helpers for the engine's component serializers. These centralise the
/// conventions used across the 3D serializers so each one does not have to re-implement
/// <see cref="Vector3"/>, <see cref="Quaternion"/> and <see cref="Color"/> parsing.
/// </summary>
/// <remarks>
/// Conventions mirror the existing 2D serializers:
/// <list type="bullet">
///   <item><see cref="Vector3"/> is read from <c>[x, y, z]</c> or <c>{ "x", "y", "z" }</c>.</item>
///   <item><see cref="Quaternion"/> is read from <c>[x, y, z, w]</c> or <c>{ "x", "y", "z", "w" }</c>.</item>
///   <item><see cref="Color"/> is read from a 3- (RGB) or 4-element (RGBA) integer array, matching
///     the <c>Sprite</c> tint format, and is always written as a 4-element RGBA array (so saved
///     JSON includes the alpha channel even when an example only shows RGB).</item>
/// </list>
/// All readers throw <see cref="PrefabLoadException"/> on malformed input, with the offending
/// property name in the message.
/// </remarks>
internal static class ComponentJson
{
    public static float ReadSingle(JsonElement el, string propertyName)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetSingle(out var value))
            throw new PrefabLoadException($"Property '{propertyName}' must be a number.");

        return value;
    }

    public static float GetOptionalSingle(
        JsonElement element,
        string propertyName,
        float defaultValue
    ) =>
        element.TryGetProperty(propertyName, out var el)
            ? ReadSingle(el, propertyName)
            : defaultValue;

    public static bool GetOptionalBoolean(
        JsonElement element,
        string propertyName,
        bool defaultValue
    )
    {
        if (!element.TryGetProperty(propertyName, out var el))
            return defaultValue;

        if (el.ValueKind != JsonValueKind.True && el.ValueKind != JsonValueKind.False)
            throw new PrefabLoadException($"Property '{propertyName}' must be a boolean.");

        return el.GetBoolean();
    }

    /// <summary>
    /// Reads an optional string property. Returns <paramref name="defaultValue"/> when the property
    /// is absent, JSON <c>null</c>, or blank (empty/whitespace); throws when present but not a
    /// string. Blank strings are treated as "unset" so that asset-path fields never round-trip a
    /// whitespace-only path that downstream consumers (which only check <c>IsNullOrEmpty</c>) would
    /// mistake for a real reference.
    /// </summary>
    public static string? GetOptionalString(
        JsonElement element,
        string propertyName,
        string? defaultValue
    )
    {
        if (!element.TryGetProperty(propertyName, out var el))
            return defaultValue;

        if (el.ValueKind == JsonValueKind.Null)
            return defaultValue;

        if (el.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException($"Property '{propertyName}' must be a string.");

        var value = el.GetString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public static Vector3 ReadVector3(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var arr = el.EnumerateArray().ToArray();
            if (arr.Length != 3)
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must be a Vector3 array with exactly three numeric elements."
                );

            return new Vector3(
                ReadSingle(arr[0], $"{propertyName}[0]"),
                ReadSingle(arr[1], $"{propertyName}[1]"),
                ReadSingle(arr[2], $"{propertyName}[2]")
            );
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            return new Vector3(
                ReadSingle(GetRequired(el, propertyName, "x"), $"{propertyName}.x"),
                ReadSingle(GetRequired(el, propertyName, "y"), $"{propertyName}.y"),
                ReadSingle(GetRequired(el, propertyName, "z"), $"{propertyName}.z")
            );
        }

        throw new PrefabLoadException(
            $"Property '{propertyName}' must be a Vector3 represented as [x, y, z] or {{ \"x\": number, \"y\": number, \"z\": number }}."
        );
    }

    public static Vector3 GetOptionalVector3(
        JsonElement element,
        string propertyName,
        Vector3 defaultValue
    ) =>
        element.TryGetProperty(propertyName, out var el)
            ? ReadVector3(el, propertyName)
            : defaultValue;

    public static Quaternion ReadQuaternion(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var arr = el.EnumerateArray().ToArray();
            if (arr.Length != 4)
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must be a Quaternion array with exactly four numeric elements [x, y, z, w]."
                );

            return new Quaternion(
                ReadSingle(arr[0], $"{propertyName}[0]"),
                ReadSingle(arr[1], $"{propertyName}[1]"),
                ReadSingle(arr[2], $"{propertyName}[2]"),
                ReadSingle(arr[3], $"{propertyName}[3]")
            );
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            return new Quaternion(
                ReadSingle(GetRequired(el, propertyName, "x"), $"{propertyName}.x"),
                ReadSingle(GetRequired(el, propertyName, "y"), $"{propertyName}.y"),
                ReadSingle(GetRequired(el, propertyName, "z"), $"{propertyName}.z"),
                ReadSingle(GetRequired(el, propertyName, "w"), $"{propertyName}.w")
            );
        }

        throw new PrefabLoadException(
            $"Property '{propertyName}' must be a Quaternion represented as [x, y, z, w] or {{ \"x\": number, \"y\": number, \"z\": number, \"w\": number }}."
        );
    }

    public static Quaternion GetOptionalQuaternion(
        JsonElement element,
        string propertyName,
        Quaternion defaultValue
    ) =>
        element.TryGetProperty(propertyName, out var el)
            ? ReadQuaternion(el, propertyName)
            : defaultValue;

    public static Color ReadColor(JsonElement el, string propertyName)
    {
        if (el.ValueKind != JsonValueKind.Array)
            throw new PrefabLoadException(
                $"Property '{propertyName}' must be a 'color' array of 3 (RGB) or 4 (RGBA) integers."
            );

        Span<int> channels = stackalloc int[4];
        channels[3] = 255;
        var count = 0;
        foreach (var channelEl in el.EnumerateArray())
        {
            if (count == 4)
                throw new PrefabLoadException(
                    $"Property '{propertyName}' must contain 3 (RGB) or 4 (RGBA) elements."
                );

            if (
                !channelEl.TryGetInt32(out var channelValue)
                || channelValue < 0
                || channelValue > 255
            )
                throw new PrefabLoadException(
                    $"Property '{propertyName}' elements must be integers between 0 and 255."
                );

            channels[count++] = channelValue;
        }

        if (count < 3)
            throw new PrefabLoadException(
                $"Property '{propertyName}' must contain 3 (RGB) or 4 (RGBA) elements."
            );

        return new Color(
            (byte)channels[0],
            (byte)channels[1],
            (byte)channels[2],
            (byte)channels[3]
        );
    }

    public static Color GetOptionalColor(
        JsonElement element,
        string propertyName,
        Color defaultValue
    ) =>
        element.TryGetProperty(propertyName, out var el)
            ? ReadColor(el, propertyName)
            : defaultValue;

    public static JsonArray Write(Vector3 v) =>
        new(JsonValue.Create(v.X), JsonValue.Create(v.Y), JsonValue.Create(v.Z));

    public static JsonArray Write(Quaternion q) =>
        new(
            JsonValue.Create(q.X),
            JsonValue.Create(q.Y),
            JsonValue.Create(q.Z),
            JsonValue.Create(q.W)
        );

    public static JsonArray Write(Color c) => new(c.R, c.G, c.B, c.A);

    private static JsonElement GetRequired(JsonElement el, string propertyName, string field)
    {
        if (!el.TryGetProperty(field, out var fieldEl))
            throw new PrefabLoadException(
                $"Property '{propertyName}' must contain a '{field}' number."
            );

        return fieldEl;
    }
}

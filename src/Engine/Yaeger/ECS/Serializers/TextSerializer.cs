using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Text"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Text",
///   "content": "Score: 0",
///   "font": "Assets/Roboto-Regular.ttf",
///   "fontSize": 24,
///   "color": [255, 255, 255, 255]
/// }
/// </code>
/// <c>content</c>, <c>font</c>, and <c>fontSize</c> are required. <c>font</c> is a path resolved
/// the same way a <see cref="Sprite"/>'s <c>texturePath</c> is, and is loaded into a
/// <see cref="FontHandle"/> — the component only ever round-trips the handle, never a native
/// <c>Yaeger.Font.Font</c> instance, matching how <c>UnifiedRenderSystem</c> resolves fonts by
/// path at render time. <c>color</c> is optional and defaults to white (255, 255, 255, 255).
/// <para>
/// This serializer duplicates a small amount of colour-parsing logic that the shared
/// <c>ComponentJson</c>/<c>ComponentJson2D</c> helpers already have, rather than using them:
/// those types are <c>internal</c> to <c>Yaeger.Core</c> (where the rest of <c>ECS/Serializers</c>
/// compiles via a linked-file glob), but <see cref="TextSerializer"/> is compiled directly into
/// the native <c>Yaeger</c> assembly instead — see <c>Yaeger.Core.csproj</c> — so those internal
/// helpers aren't visible here.
/// </para>
/// </remarks>
public sealed class TextSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Text";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Text);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var content = ReadRequiredString(element, "content");
        var fontPath = ReadRequiredString(element, "font");
        var fontSize = ReadRequiredInt(element, "fontSize");
        var color = ReadOptionalColor(element, "color", Color.White);

        FontHandle fontHandle;
        try
        {
            fontHandle = new FontHandle(fontPath);
        }
        catch (ArgumentException ex)
        {
            throw new PrefabLoadException($"Text has an invalid 'font' property: {ex.Message}", ex);
        }

        var component = new Text(content, fontHandle, fontSize, color);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Text>(entity, out var text))
            return null;

        var obj = new JsonObject
        {
            ["type"] = TypeId,
            ["content"] = text.Content,
            ["font"] = text.FontHandle.Id,
            ["fontSize"] = text.FontSize,
        };

        if (!IsWhite(text.Color))
            obj["color"] = new JsonArray(text.Color.R, text.Color.G, text.Color.B, text.Color.A);

        return obj;
    }

    private static bool IsWhite(Color c) => c.R == 255 && c.G == 255 && c.B == 255 && c.A == 255;

    private static Color ReadOptionalColor(
        JsonElement element,
        string propertyName,
        Color defaultValue
    )
    {
        if (!element.TryGetProperty(propertyName, out var el))
            return defaultValue;

        if (el.ValueKind != JsonValueKind.Array)
            throw new PrefabLoadException(
                $"Text '{propertyName}' must be an array of 3 (RGB) or 4 (RGBA) integers."
            );

        Span<int> channels = stackalloc int[4];
        channels[3] = 255;
        var count = 0;
        foreach (var channelEl in el.EnumerateArray())
        {
            if (count == 4)
                throw new PrefabLoadException(
                    $"Text '{propertyName}' must contain 3 (RGB) or 4 (RGBA) elements."
                );

            if (
                !channelEl.TryGetInt32(out var channelValue)
                || channelValue < 0
                || channelValue > 255
            )
                throw new PrefabLoadException(
                    $"Text '{propertyName}' elements must be integers between 0 and 255."
                );

            channels[count++] = channelValue;
        }

        if (count < 3)
            throw new PrefabLoadException(
                $"Text '{propertyName}' must contain 3 (RGB) or 4 (RGBA) elements."
            );

        return new Color(
            (byte)channels[0],
            (byte)channels[1],
            (byte)channels[2],
            (byte)channels[3]
        );
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (
            !element.TryGetProperty(propertyName, out var el)
            || el.ValueKind != JsonValueKind.String
        )
            throw new PrefabLoadException(
                $"Text is missing required '{propertyName}' string property."
            );

        return el.GetString() ?? string.Empty;
    }

    private static int ReadRequiredInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var el) || !el.TryGetInt32(out var value))
            throw new PrefabLoadException(
                $"Text is missing required '{propertyName}' integer property."
            );

        return value;
    }
}

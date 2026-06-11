using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Fluent helper for creating UI entities with window-relative positioning.
/// Pass the current window size so that <see cref="Width"/>, <see cref="Height"/>,
/// <see cref="CenterX"/>, and <see cref="CenterY"/> stay correct across resolutions.
/// </summary>
public class UiBuilder(World world, Vector2 windowSize)
{
    /// <summary>Returns a pixel width equal to <paramref name="fraction"/> of the window width.</summary>
    public float Width(float fraction) => windowSize.X * fraction;

    /// <summary>Returns a pixel height equal to <paramref name="fraction"/> of the window height.</summary>
    public float Height(float fraction) => windowSize.Y * fraction;

    /// <summary>Returns the X pixel position that horizontally centres an element of <paramref name="elementWidth"/>.</summary>
    public float CenterX(float elementWidth) => (windowSize.X - elementWidth) / 2f;

    /// <summary>Returns the Y pixel position that vertically centres an element of <paramref name="elementHeight"/>.</summary>
    public float CenterY(float elementHeight) => (windowSize.Y - elementHeight) / 2f;

    /// <summary>Creates a panel entity at the given pixel coordinates.</summary>
    public Entity CreatePanel(
        float x,
        float y,
        float width,
        float height,
        Color backgroundColor,
        float borderRadius = 0f,
        string? tag = null
    )
    {
        var entity = tag is not null ? world.CreateEntity(tag) : world.CreateEntity();
        world.AddComponent(
            entity,
            new UiRect { Position = new Vector2(x, y), Size = new Vector2(width, height) }
        );
        world.AddComponent(
            entity,
            new UiPanel { BackgroundColor = backgroundColor, BorderRadius = borderRadius }
        );
        return entity;
    }

    /// <summary>Creates a button entity at the given pixel coordinates with normal/hovered/pressed colours.</summary>
    public Entity CreateButton(
        float x,
        float y,
        float width,
        float height,
        Color normal,
        Color hovered,
        Color pressed,
        string? tag = null
    )
    {
        var entity = tag is not null ? world.CreateEntity(tag) : world.CreateEntity();
        world.AddComponent(
            entity,
            new UiRect { Position = new Vector2(x, y), Size = new Vector2(width, height) }
        );
        world.AddComponent(
            entity,
            new UiButton { Normal = normal, Hovered = hovered, Pressed = pressed }
        );
        world.AddComponent(entity, new UiButtonState());
        return entity;
    }

    /// <summary>Creates a label entity positioned at the given pixel coordinates.</summary>
    public Entity CreateLabel(
        float x,
        float y,
        string text,
        float fontSize,
        Color color,
        string? tag = null
    )
    {
        var entity = tag is not null ? world.CreateEntity(tag) : world.CreateEntity();
        world.AddComponent(
            entity,
            new UiRect { Position = new Vector2(x, y), Size = Vector2.Zero }
        );
        world.AddComponent(
            entity,
            new UiLabel { Text = text, FontSize = fontSize, Color = color }
        );
        return entity;
    }
}

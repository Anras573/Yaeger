using System.Diagnostics.CodeAnalysis;

namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public record struct Text(string Content, Font.Font Font, int FontSize, Color Color);
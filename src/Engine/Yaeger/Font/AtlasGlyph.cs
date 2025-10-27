using System.Numerics;

namespace Yaeger.Font;

/// <summary>
/// Represents a glyph in the atlas with its texture coordinates and metrics.
/// </summary>
public record struct AtlasGlyph(uint Codepoint, Vector2 TexCoordMin, Vector2 TexCoordMax, Vector2 Size, Vector2 Bearing, float Advance);
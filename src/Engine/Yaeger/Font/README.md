# Font Rendering with HarfBuzz

This directory contains the HarfBuzz integration for the Yaeger game engine, preparing it for font rendering capabilities.

## Components

### Font
The `Font` class represents a loaded font file and provides access to the underlying HarfBuzz font object.

**Example usage:**
```csharp
using var font = new Font("path/to/font.ttf");
```

### FontManager
The `FontManager` class manages font resources, handling loading, caching, and disposal of fonts.

**Example usage:**
```csharp
using var fontManager = new FontManager();
var font = fontManager.Load("path/to/font.ttf");
```

### TextShaper
The `TextShaper` class wraps HarfBuzz's text shaping functionality, converting text strings into positioned glyphs.

**Example usage:**
```csharp
var font = new Font("path/to/font.ttf");
var shaper = new TextShaper(font);
var glyphs = shaper.Shape("Hello, World!");

// Each glyph contains:
// - Codepoint: The glyph index in the font
// - XAdvance/YAdvance: How far to advance the cursor
// - XOffset/YOffset: Positioning adjustments
// - Cluster: Original character cluster index
```

### Advanced Text Shaping
For advanced typography features (ligatures, kerning, etc.):

```csharp
using HarfBuzzSharp;

var features = new[]
{
    new Feature(Tag.Parse("liga"), 1, 0, uint.MaxValue), // Enable ligatures
    new Feature(Tag.Parse("kern"), 1, 0, uint.MaxValue)  // Enable kerning
};

var glyphs = shaper.ShapeWithFeatures("Hello, World!", features);
```

## Dependencies

This integration uses the **HarfBuzzSharp** package (version 8.3.1.2), which includes:
- HarfBuzzSharp - C# bindings for HarfBuzz
- Native HarfBuzz libraries for Windows, macOS, and Linux

## Future Integration

The classes in this directory provide the foundation for font rendering. Future work will include:
1. Glyph atlas generation (texture containing rendered glyphs)
2. Text rendering system using the existing OpenGL renderer
3. Font shader implementation for text rendering
4. Text component for the ECS system

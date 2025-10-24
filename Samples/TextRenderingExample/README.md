# Text Rendering Example

This example demonstrates text rendering capabilities in the Yaeger game engine.

## Features

- Font loading and management
- Text shaping with HarfBuzz
- Glyph atlas generation
- Efficient batch text rendering
- ECS integration with Text component

## How to Run

```bash
dotnet run --project Samples/TextRenderingExample/TextRenderingExample.csproj
```

**Note**: This sample requires:
- A display/window system (will not run in headless environments)
- A TrueType font file (.ttf) in the Assets directory

## What This Example Shows

1. **Font Loading**: Loading a TrueType font using the Font class
2. **Text Component**: Creating text entities in the ECS world
3. **Glyph Atlas**: Automatic generation of glyph texture atlases
4. **Text Rendering**: Efficient rendering of text using batch rendering
5. **Text Shaping**: Proper text layout with HarfBuzz

## Implementation Details

The example demonstrates the complete text rendering pipeline:

### 1. Font Loading
```csharp
var fontManager = new FontManager();
var font = fontManager.Load("path/to/font.ttf");
```

### 2. Creating Text Entities
```csharp
var entity = world.CreateEntity();
world.AddComponent(entity, new Text("Hello, World!", font, fontSize: 48));
world.AddComponent(entity, new Transform2D { Position = new Vector2(0, 0) });
```

### 3. Rendering Text
```csharp
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);
textRenderSystem.Render();
```

## Architecture

The text rendering system consists of several key components:

### Font Infrastructure (`Yaeger.Font`)
- **Font**: Manages HarfBuzz font objects
- **FontManager**: Caches and manages font resources
- **TextShaper**: Shapes text into positioned glyphs
- **GlyphAtlas**: Generates and manages glyph texture atlases

### Rendering (`Yaeger.Rendering`)
- **TextRenderer**: Specialized renderer for text using glyph atlases

### ECS Integration (`Yaeger.Graphics`, `Yaeger.Systems`)
- **Text**: Component containing text content, font, and styling
- **TextRenderSystem**: System that renders all text entities

## Performance Notes

- Text rendering uses batch rendering to minimize draw calls
- Glyphs are cached in texture atlases for efficient rendering
- Multiple text strings sharing the same font use the same atlas
- Up to 1000 glyphs can be rendered in a single batch

## Future Enhancements

Possible future improvements:
- Advanced text formatting (bold, italic, underline)
- Multi-line text support
- Text alignment options
- Dynamic font size scaling
- Better atlas packing algorithms
- Support for emoji and complex scripts

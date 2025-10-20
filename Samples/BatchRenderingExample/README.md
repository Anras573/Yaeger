# Batch Rendering Example

This example demonstrates batch rendering in the Yaeger game engine.

## What is Batch Rendering?

Batch rendering is a technique that groups multiple draw calls by texture to minimize GPU state changes. Instead of making individual draw calls for each sprite, we collect all sprites that share the same texture and render them in a single draw call.

## Why is Batch Rendering Important?

1. **Performance**: Reduces CPU-GPU communication overhead
2. **Scalability**: Enables rendering thousands of sprites efficiently
3. **Font Rendering**: Essential for rendering text, where many characters share the same texture atlas
4. **Particle Systems**: Critical for rendering large numbers of particles

## How to Run

```bash
dotnet run --project Samples/BatchRenderingExample/BatchRenderingExample.csproj
```

**Note**: This sample requires a display/window system and will not run in headless environments.

## Controls

- **SPACE**: Toggle between batch rendering and individual rendering
- **ESC**: Exit the application

## Implementation Details

The example demonstrates two rendering approaches:

### Individual Rendering (Traditional)
Each sprite is rendered with its own draw call:
```csharp
renderer.BeginFrame();
foreach (var sprite in sprites)
{
    renderer.DrawQuad(transform, texturePath);
}
renderer.EndFrame();
```

### Batch Rendering (Optimized)
Sprites are grouped by texture and rendered together:
```csharp
batchRenderer.BeginFrame();
foreach (var sprite in sprites)
{
    batchRenderer.SubmitQuad(transform, texturePath);
}
batchRenderer.EndFrame(); // All batches are rendered here
```

## Performance Comparison

With 500 sprites using the same texture:
- **Individual Rendering**: 500 draw calls
- **Batch Rendering**: 1 draw call

You can toggle between the two modes during runtime to observe the performance difference in the FPS counter.

## Key Components

### BatchRenderer
The `BatchRenderer` class (`Yaeger.Rendering.BatchRenderer`) implements the batching logic:

1. **Submission Phase**: Collects quads grouped by texture
2. **Batching Phase**: Fills dynamic vertex buffers with transformed vertices
3. **Rendering Phase**: Issues a single draw call per texture per batch (max 1000 quads)

### Dynamic Vertex Buffers
The renderer uses dynamic VBOs that are updated each frame with the transformed vertex data, avoiding the need for matrix uniforms in the shader.

## Future Enhancements

This example provides a foundation for:
- **Font Rendering**: Using texture atlases with batched character quads
- **Sprite Atlases**: Rendering multiple sprite types from a single texture
- **Particle Systems**: Efficient rendering of thousands of particles
- **UI Systems**: Fast rendering of UI elements

# Animation System

The animation system allows you to create sprite animations in Yaeger using the ECS architecture. An animation is a collection of frames, where each frame consists of a texture and a duration that specifies how long the texture should be displayed.

## Components

### Animation
The `Animation` component defines an animation as a collection of frames. Each frame contains a texture path and duration.

```csharp
using Yaeger.Graphics;

// Create animation frames
var frames = new AnimationFrame[]
{
    new AnimationFrame("Assets/frame1.png", 0.1f), // Display for 0.1 seconds
    new AnimationFrame("Assets/frame2.png", 0.1f),
    new AnimationFrame("Assets/frame3.png", 0.1f),
    new AnimationFrame("Assets/frame4.png", 0.2f)  // Display for 0.2 seconds
};

// Create the animation (looping by default)
var animation = new Animation(frames, loop: true);

// Create a non-looping animation
var nonLoopingAnimation = new Animation(frames, loop: false);
```

### AnimationState
The `AnimationState` component tracks the current state of an animation, including the current frame index and elapsed time.

```csharp
// Initialize animation state (starts at frame 0)
var animState = new AnimationState();

// Or specify a starting frame
var animState = new AnimationState(CurrentFrameIndex: 2, ElapsedTime: 0f);
```

## System

### AnimationSystem
The `AnimationSystem` updates all entities that have both `Animation` and `AnimationState` components. It automatically:
- Advances frames based on elapsed time
- Loops the animation (if enabled)
- Updates the entity's `Sprite` component with the current frame's texture
- Marks non-looping animations as finished

## Usage Example

Here's a complete example showing how to create an animated entity:

```csharp
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

using var window = Window.Create();
var world = new World();
var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);
var animationSystem = new AnimationSystem(world);

// Create an animated entity
var entity = world.CreateEntity();

// Add a transform (position, rotation, scale)
world.AddComponent(entity, new Transform2D(
    position: new Vector2(0, 0),
    rotation: 0f,
    scale: new Vector2(0.5f, 0.5f)
));

// Create animation frames
var frames = new AnimationFrame[]
{
    new AnimationFrame("Assets/walk1.png", 0.1f),
    new AnimationFrame("Assets/walk2.png", 0.1f),
    new AnimationFrame("Assets/walk3.png", 0.1f),
    new AnimationFrame("Assets/walk4.png", 0.1f)
};

// Add animation and initial state
world.AddComponent(entity, new Animation(frames, loop: true));
world.AddComponent(entity, new AnimationState());

// Add initial sprite (will be updated by AnimationSystem)
world.AddComponent(entity, new Sprite(frames[0].TexturePath));

// Game loop
window.OnUpdate += deltaTime =>
{
    // Update animations
    animationSystem.Update((float)deltaTime);
};

window.OnRender += _ =>
{
    // Render all entities with sprites
    renderSystem.Render();
};

window.Run();
```

## Integration with Existing Code

The animation system integrates seamlessly with the existing ECS architecture:

1. **Add the AnimationSystem**: Create an instance of `AnimationSystem` and call its `Update` method in your game loop before rendering.

2. **Create Animated Entities**: Add `Animation`, `AnimationState`, and `Sprite` components to entities that should be animated.

3. **AnimationSystem Updates Sprites**: The system automatically updates each entity's `Sprite` component based on the current animation frame.

4. **RenderSystem Renders**: The existing `RenderSystem` renders the updated sprites normally.

## Features

- **Frame-based animation**: Define individual frames with specific durations
- **Looping**: Animations can loop infinitely or play once
- **Non-blocking**: Finished non-looping animations stay on the last frame
- **ECS integration**: Works seamlessly with other ECS components
- **Automatic sprite updating**: No manual texture swapping needed

## API Reference

### AnimationFrame

```csharp
public readonly record struct AnimationFrame(string texturePath, float duration)
{
    public string TexturePath { get; init; }  // Path to the texture file
    public float Duration { get; init; }      // Duration in seconds (must be > 0)
}
```

### Animation

```csharp
public readonly record struct Animation
{
    public AnimationFrame[] Frames { get; }
    public bool Loop { get; }
    public float TotalDuration { get; }  // Calculated property
    
    public Animation(AnimationFrame[] frames, bool loop = true)
}
```

### AnimationState

```csharp
public record struct AnimationState
{
    public int CurrentFrameIndex { get; set; }
    public float ElapsedTime { get; set; }
    public bool IsFinished { get; set; }
    
    public AnimationState(int CurrentFrameIndex = 0, float ElapsedTime = 0f, bool IsFinished = false)
}
```

### AnimationSystem

```csharp
public class AnimationSystem(World world)
{
    public void Update(float deltaTime)
}
```

## Advanced Usage

### Changing Animations at Runtime

You can change an entity's animation by updating its `Animation` component:

```csharp
// Switch to idle animation
var idleFrames = new AnimationFrame[]
{
    new AnimationFrame("Assets/idle1.png", 0.2f),
    new AnimationFrame("Assets/idle2.png", 0.2f)
};
world.AddComponent(entity, new Animation(idleFrames));
world.AddComponent(entity, new AnimationState()); // Reset state
```

### Checking Animation Completion

For non-looping animations, you can check if they've finished:

```csharp
if (world.TryGetComponent<AnimationState>(entity, out var state) && state.IsFinished)
{
    // Animation has finished, do something
    Console.WriteLine("Animation complete!");
}
```

### Controlling Animation Speed

You can control animation speed by scaling the delta time:

```csharp
// Play animation at half speed
animationSystem.Update((float)deltaTime * 0.5f);

// Play animation at double speed
animationSystem.Update((float)deltaTime * 2.0f);
```

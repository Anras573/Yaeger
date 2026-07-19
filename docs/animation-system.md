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
// UnifiedRenderSystem draws sprites (and optionally text) in deterministic layer order.
// Pass null for the text renderer when you only need sprites.
var renderSystem = new UnifiedRenderSystem(renderer, null, world);
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

4. **UnifiedRenderSystem Renders**: `UnifiedRenderSystem` renders the updated sprites normally. (The older `RenderSystem` is now `[Obsolete]` — prefer `UnifiedRenderSystem`.)

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

You can change an entity's animation by updating its `Animation` component directly:

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

For more than a couple of named states, `AnimationStateMachine` (below) turns this manual
swap-both-components pattern into a declarative `Play("idle")` call.

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

## Sprite Flipping

`Sprite.FlipX`/`Sprite.FlipY` mirror the rendered image horizontally/vertically without touching
`Transform2D` — the classic platformer "face left/right" need, decoupled from scale so it doesn't
conflate facing with actual size or leak into colliders. The renderer implements this by swapping
the submitted quad's UV coordinates, so a flipped sprite stays on the same batched draw path as an
unflipped one (no extra draw calls).

```csharp
// A character facing left, at normal size.
world.AddComponent(entity, new Sprite("Assets/player.png", flipX: true));
```

For an entity animated via `SpriteSheet` + `AnimationState` rather than a plain `Sprite`, attach a
`Sprite` alongside them purely to carry the flip flags — `UnifiedRenderSystem` reads it for flip
state only; its `TexturePath` is ignored in that case (the `SpriteSheet`'s texture is authoritative):

```csharp
world.AddComponent(entity, spriteSheet);
world.AddComponent(entity, animationState);
world.AddComponent(entity, new Sprite("unused", flipX: facingLeft));
```

`AnimationSystem` preserves `FlipX`/`FlipY` (and `Tint`) on the `Sprite` component it writes back
each time a plain-`Sprite` animation advances a frame, so flip state survives frame changes.

`Sprite` serializes `flipX`/`flipY` as optional JSON fields (omitted when `false`):

```json
{ "type": "Sprite", "texturePath": "Assets/player.png", "flipX": true }
```

## AnimationStateMachine

`AnimationStateMachine` is a small helper for switching between named animation states (idle, run,
jump, fall, ...) without game code manually swapping `Animation`/`AnimationState` components. A
"state" is simply a name mapped to an `Animation` clip — the same clip type that already drives
frame timing for both `Sprite` and `SpriteSheet` rendering paths. It is deliberately minimal: no
blend trees, no transition-condition DSL. Game code decides *when* to switch (by calling `Play`);
the helper only handles switching cleanly (resetting the frame timer, swapping the clip).

```csharp
using Yaeger.Graphics;
using Yaeger.Systems;

var states = new Dictionary<string, Animation>
{
    ["idle"] = new(idleFrames, loop: true),
    ["jump"] = new(jumpFrames, loop: false),
};

var entity = world.CreateEntity();
world.AddComponent(entity, new AnimationStateMachine(states, initialState: "idle"));

var stateMachineSystem = new AnimationStateMachineSystem(world);
var animationSystem = new AnimationSystem(world);

// Game code decides when to switch:
stateMachineSystem.Play(entity, "jump");

// Run the state machine system BEFORE AnimationSystem each frame, so a switch requested
// this frame takes effect immediately rather than advancing the old clip one more step first.
window.OnUpdate += deltaTime =>
{
    stateMachineSystem.Update((float)deltaTime);
    animationSystem.Update((float)deltaTime);
};
```

Switching to a different state always restarts it at frame 0. Calling `Play` again with the name
of the state that's already active does *not* restart it — unless `RestartOnReplay` is set to
`true` at construction — so re-confirming "yes, still idle" every frame doesn't visibly stutter a
looping animation. Completion of a non-looping state (e.g. "jump" landing, "attack" finishing) is
reported the same way any `Animation` reports it: read `AnimationState.IsFinished` on the entity.

`AnimationStateMachine` serializes to/from prefab and scene JSON:

```json
{
  "type": "AnimationStateMachine",
  "currentState": "idle",
  "restartOnReplay": false,
  "states": {
    "idle": { "loop": true, "frames": [{ "texturePath": "Assets/idle0.png", "duration": 0.2 }] },
    "jump": { "loop": false, "frames": [{ "texturePath": "Assets/jump0.png", "duration": 0.15 }] }
  }
}
```

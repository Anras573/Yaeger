using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// System that updates animations and changes sprite textures based on the current animation frame.
/// </summary>
public class AnimationSystem(World world)
{
    /// <summary>
    /// Updates all entities that have both Animation and AnimationState components.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last update, in seconds.</param>
    public void Update(float deltaTime)
    {
        foreach ((Entity entity, Animation animation, AnimationState state) in world.Query<Animation, AnimationState>())
        {
            // Skip if animation is finished and not looping
            if (state.IsFinished && !animation.Loop)
            {
                continue;
            }

            // Validate frame index to prevent crashes from manually modified state
            var currentState = state;
            if (currentState.CurrentFrameIndex < 0 || currentState.CurrentFrameIndex >= animation.Frames.Length)
            {
                // Reset to first frame if invalid
                currentState = new AnimationState(0, 0f, false);
            }

            // Create a mutable copy of the state
            var newState = currentState;
            var oldFrameIndex = currentState.CurrentFrameIndex;

            // Update elapsed time
            var newElapsedTime = newState.ElapsedTime + deltaTime;
            var currentFrameIndex = newState.CurrentFrameIndex;
            var currentFrame = animation.Frames[currentFrameIndex];

            // Check if we need to advance to the next frame
            while (newElapsedTime >= currentFrame.Duration)
            {
                newElapsedTime -= currentFrame.Duration;
                var nextFrameIndex = currentFrameIndex + 1;

                // Handle end of animation
                if (nextFrameIndex >= animation.Frames.Length)
                {
                    if (animation.Loop)
                    {
                        // Loop back to the first frame
                        nextFrameIndex = 0;
                    }
                    else
                    {
                        // Animation finished, stay on last frame
                        nextFrameIndex = animation.Frames.Length - 1;
                        newState.IsFinished = true;
                        // Clamp elapsed time to the duration of the final frame to avoid storing an excessively large value
                        newElapsedTime = currentFrame.Duration;
                        break;
                    }
                }

                currentFrameIndex = nextFrameIndex;
                currentFrame = animation.Frames[currentFrameIndex];
            }

            // Update the animation state
            newState.CurrentFrameIndex = currentFrameIndex;
            newState.ElapsedTime = newElapsedTime;
            world.AddComponent(entity, newState);

            // Only update the sprite texture if the frame has changed
            if (currentFrameIndex != oldFrameIndex)
            {
                var sprite = new Sprite(currentFrame.TexturePath);
                world.AddComponent(entity, sprite);
            }
        }
    }
}
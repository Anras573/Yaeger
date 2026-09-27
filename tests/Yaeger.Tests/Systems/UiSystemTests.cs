using System.Numerics;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Platform;
using Yaeger.Systems;
using Yaeger.UI;

namespace Yaeger.Tests.Systems;

public class UiSystemTests
{
    private sealed class FakeInputState : IInputState
    {
        public Vector2 MousePosition { get; set; }
        public bool MouseButtonPressed { get; set; }

        public bool IsKeyPressed(Keys key) => false;

        public bool IsMouseButtonPressed(MouseButton button) =>
            button == MouseButton.Left && MouseButtonPressed;

        public Vector2 MousePositionNdc => Vector2.Zero;
        public float ScrollDelta => 0f;
    }

    private static (World world, Entity button, UiSystem system, FakeInputState input) CreateScene(
        float x,
        float y,
        float w,
        float h
    )
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new UiRect { Position = new Vector2(x, y), Size = new Vector2(w, h) }
        );
        world.AddComponent(entity, new UiButton());
        var input = new FakeInputState();
        return (world, entity, new UiSystem(world, input), input);
    }

    [Fact]
    public void Update_WhenMouseOutsideButton_ShouldNotSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(50, 50);

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.False(state.IsHovered);
        Assert.False(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseInsideButton_ShouldSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.True(state.IsHovered);
        Assert.False(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseOnTopLeftCorner_ShouldSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(100, 100); // inclusive left/top edge

        system.Update(0f);

        Assert.True(world.GetComponent<UiButtonState>(entity).IsHovered);
    }

    [Fact]
    public void Update_WhenMouseOnBottomRightEdge_ShouldNotSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(300, 150); // exclusive right/bottom edge

        system.Update(0f);

        Assert.False(world.GetComponent<UiButtonState>(entity).IsHovered);
    }

    [Fact]
    public void Update_WhenMousePressedInsideButton_ShouldSetIsPressed()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);
        input.MouseButtonPressed = true;

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.True(state.IsHovered);
        Assert.True(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseReleasedOverButton_ShouldSetWasClicked()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        input.MouseButtonPressed = true;
        system.Update(0f); // press frame

        input.MouseButtonPressed = false;
        system.Update(0f); // release frame

        Assert.True(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenMouseReleasedOutsideButton_ShouldNotSetWasClicked()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);

        input.MousePosition = new Vector2(150, 120);
        input.MouseButtonPressed = true;
        system.Update(0f); // press inside

        input.MousePosition = new Vector2(50, 50); // move outside before release
        input.MouseButtonPressed = false;
        system.Update(0f); // release outside

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenPressStartedOutsideButton_ShouldNotSetWasClicked()
    {
        // Regression: holding mouse down elsewhere and dragging into a button must not click.
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);

        input.MousePosition = new Vector2(50, 50); // outside
        input.MouseButtonPressed = true;
        system.Update(0f); // press started outside — button not added to _pressStartedOn

        input.MousePosition = new Vector2(150, 120); // drag inside while still pressed
        system.Update(0f); // isHovered=true, but pressStartedThisFrame=false → not registered

        input.MouseButtonPressed = false;
        system.Update(0f); // release inside — must NOT fire WasClicked

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WasClicked_ShouldBeTrueForExactlyOneFrame()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        input.MouseButtonPressed = true;
        system.Update(0f);

        input.MouseButtonPressed = false;
        system.Update(0f); // release frame — WasClicked must be true here
        Assert.True(world.GetComponent<UiButtonState>(entity).WasClicked);

        system.Update(0f); // next frame — must revert to false
        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }
}

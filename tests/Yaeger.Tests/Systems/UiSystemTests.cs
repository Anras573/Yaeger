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
        private bool _held;
        private bool _pressedEdge;
        private bool _releasedEdge;

        public Vector2 MousePosition { get; set; }

        /// <summary>Sets the held level and records the matching press/release edge.</summary>
        public bool MouseButtonPressed
        {
            get => _held;
            set
            {
                if (value && !_held)
                    _pressedEdge = true;
                if (!value && _held)
                    _releasedEdge = true;
                _held = value;
            }
        }

        /// <summary>A press + release that both land inside one frame: never observed as held.</summary>
        public void QuickClick()
        {
            _pressedEdge = true;
            _releasedEdge = true;
        }

        /// <summary>Frame boundary: clears the edges, like BeginFrame/EndFrame on a real backend.</summary>
        public void EndFrame()
        {
            _pressedEdge = false;
            _releasedEdge = false;
        }

        public bool IsKeyPressed(Keys key) => false;

        public bool WasKeyPressed(Keys key) => false;

        public bool WasKeyReleased(Keys key) => false;

        public bool WasMouseButtonPressed(MouseButton button) =>
            button == MouseButton.Left && _pressedEdge;

        public bool WasMouseButtonReleased(MouseButton button) =>
            button == MouseButton.Left && _releasedEdge;

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

    private static void Frame(UiSystem system, FakeInputState input)
    {
        system.Update(0f);
        input.EndFrame();
    }

    [Fact]
    public void Update_WhenMouseOutsideButton_ShouldNotSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(50, 50);

        Frame(system, input);

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

        Frame(system, input);

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

        Frame(system, input);

        Assert.True(world.GetComponent<UiButtonState>(entity).IsHovered);
    }

    [Fact]
    public void Update_WhenMouseOnBottomRightEdge_ShouldNotSetIsHovered()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(300, 150); // exclusive right/bottom edge

        Frame(system, input);

        Assert.False(world.GetComponent<UiButtonState>(entity).IsHovered);
    }

    [Fact]
    public void Update_WhenMousePressedInsideButton_ShouldSetIsPressed()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);
        input.MouseButtonPressed = true;

        Frame(system, input);

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
        Frame(system, input); // press frame

        input.MouseButtonPressed = false;
        Frame(system, input); // release frame

        Assert.True(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenMouseReleasedOutsideButton_ShouldNotSetWasClicked()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);

        input.MousePosition = new Vector2(150, 120);
        input.MouseButtonPressed = true;
        Frame(system, input); // press inside

        input.MousePosition = new Vector2(50, 50); // move outside before release
        input.MouseButtonPressed = false;
        Frame(system, input); // release outside

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenPressStartedOutsideButton_ShouldNotSetWasClicked()
    {
        // Regression: holding mouse down elsewhere and dragging into a button must not click.
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);

        input.MousePosition = new Vector2(50, 50); // outside
        input.MouseButtonPressed = true;
        Frame(system, input); // press started outside — button not added to _pressStartedOn

        input.MousePosition = new Vector2(150, 120); // drag inside while still pressed
        Frame(system, input); // isHovered=true, but pressStartedThisFrame=false → not registered

        input.MouseButtonPressed = false;
        Frame(system, input); // release inside — must NOT fire WasClicked

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WasClicked_ShouldBeTrueForExactlyOneFrame()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        input.MouseButtonPressed = true;
        Frame(system, input);

        input.MouseButtonPressed = false;
        Frame(system, input); // release frame — WasClicked must be true here
        Assert.True(world.GetComponent<UiButtonState>(entity).WasClicked);

        Frame(system, input); // next frame — must revert to false
        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenPressAndReleaseLandInOneFrame_ShouldSetWasClicked()
    {
        // A tap shorter than a frame is never observed as held, only as both edges.
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        input.QuickClick();
        Frame(system, input);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.True(state.WasClicked);
        Assert.False(state.IsPressed);
    }

    [Fact]
    public void Update_WhenQuickClickOutsideButton_ShouldNotSetWasClicked()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(50, 50);

        input.QuickClick();
        Frame(system, input);

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_AfterQuickClick_ShouldNotCarryPressIntoNextFrame()
    {
        var (world, entity, system, input) = CreateScene(100, 100, 200, 50);
        input.MousePosition = new Vector2(150, 120);

        input.QuickClick();
        Frame(system, input);
        Frame(system, input);

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }
}

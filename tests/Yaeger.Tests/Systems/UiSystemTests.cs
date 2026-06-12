using System.Numerics;
using System.Reflection;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Systems;
using Yaeger.UI;

namespace Yaeger.Tests.Systems;

public class UiSystemTests : IDisposable
{
    private static readonly FieldInfo PositionField = typeof(Mouse).GetField(
        "_position",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly FieldInfo PressedButtonsField = typeof(Mouse).GetField(
        "PressedButtons",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static void SetMousePosition(Vector2 pos) => PositionField.SetValue(null, pos);

    private static void SetMouseButton(bool pressed)
    {
        var set = (HashSet<MouseButton>)PressedButtonsField.GetValue(null)!;
        if (pressed)
            set.Add(MouseButton.Left);
        else
            set.Remove(MouseButton.Left);
    }

    public void Dispose()
    {
        SetMousePosition(Vector2.Zero);
        SetMouseButton(false);
    }

    private static (World world, Entity button, UiSystem system) CreateScene(
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
        return (world, entity, new UiSystem(world));
    }

    [Fact]
    public void Update_WhenMouseOutsideButton_ShouldNotSetIsHovered()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(50, 50));

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.False(state.IsHovered);
        Assert.False(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseInsideButton_ShouldSetIsHovered()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(150, 120));

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.True(state.IsHovered);
        Assert.False(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseOnButtonEdge_ShouldSetIsHovered()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(100, 100)); // exactly top-left corner

        system.Update(0f);

        Assert.True(world.GetComponent<UiButtonState>(entity).IsHovered);
    }

    [Fact]
    public void Update_WhenMousePressedInsideButton_ShouldSetIsPressed()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(150, 120));
        SetMouseButton(true);

        system.Update(0f);

        var state = world.GetComponent<UiButtonState>(entity);
        Assert.True(state.IsHovered);
        Assert.True(state.IsPressed);
        Assert.False(state.WasClicked);
    }

    [Fact]
    public void Update_WhenMouseReleasedOverButton_ShouldSetWasClicked()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(150, 120));

        SetMouseButton(true);
        system.Update(0f); // press frame

        SetMouseButton(false);
        system.Update(0f); // release frame

        Assert.True(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WhenMouseReleasedOutsideButton_ShouldNotSetWasClicked()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);

        SetMousePosition(new Vector2(150, 120));
        SetMouseButton(true);
        system.Update(0f); // press inside

        SetMousePosition(new Vector2(50, 50)); // move outside before release
        SetMouseButton(false);
        system.Update(0f); // release outside

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }

    [Fact]
    public void Update_WasClicked_ShouldBeTrueForExactlyOneFrame()
    {
        var (world, entity, system) = CreateScene(100, 100, 200, 50);
        SetMousePosition(new Vector2(150, 120));

        SetMouseButton(true);
        system.Update(0f);

        SetMouseButton(false);
        system.Update(0f); // WasClicked = true this frame

        system.Update(0f); // next frame — should revert to false

        Assert.False(world.GetComponent<UiButtonState>(entity).WasClicked);
    }
}

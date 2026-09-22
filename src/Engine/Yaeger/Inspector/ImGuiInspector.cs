using Silk.NET.OpenGL.Extensions.ImGui;
using Yaeger.ECS;
using Yaeger.Windowing;

namespace Yaeger.Inspector;

/// <summary>
/// In-game ImGui overlay for live entity inspection and editing. Lists every entity in the world
/// and provides curated editors for both the 2D components (<see cref="Transform2D"/>,
/// <see cref="Camera2D"/>, <see cref="Sprite"/>) and the 3D components (<see cref="Transform3D"/>,
/// <see cref="Camera3D"/>, <see cref="Material3D"/>, <see cref="MeshHandle"/>,
/// <see cref="DirectionalLight"/>, <see cref="PointLight"/>, <see cref="SpotLight"/>), so it doubles
/// as a lightweight 3D scene editor. Edits are committed to the world at the end of the
/// inspector's render pass; with the recommended ordering below (scene first, overlay last) the
/// change becomes visible in the scene on the next frame.
/// Wire it up in your render loop and toggle with a key binding:
/// <code>
/// var inspector = new ImGuiInspector(window, world, componentRegistry);
/// window.OnRender += delta => { meshRenderSystem.Render(); inspector.Render(delta); };
/// Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
/// </code>
/// The inspector renders after your game systems so the overlay sits on top.
/// </summary>
/// <remarks>
/// Split by concern across partial files: this one holds construction and the top-level render
/// loop. See <c>ImGuiInspector.Gizmos.cs</c> (world-space selection gizmos + viewport
/// picking/dragging), <c>ImGuiInspector.MainWindow.cs</c> (the ImGui panel's layout — entity list,
/// component column, save row), <c>ImGuiInspector.Hierarchy.cs</c> (drag-and-drop reparenting in
/// the entity tree), <c>ImGuiInspector.ComponentEditors2D.cs</c>/
/// <c>ImGuiInspector.ComponentEditors3D.cs</c> (the curated per-component-type editors),
/// <c>ImGuiInspector.AddComponent.cs</c> (the "Add Component" row), and
/// <c>ImGuiInspector.Commands.cs</c> (the deferred add/remove/destroy command queue).
/// </remarks>
public sealed partial class ImGuiInspector : IDisposable
{
    private readonly World _world;
    private readonly Window _window;
    private readonly ComponentRegistry? _registry;
    private readonly SceneSaver? _sceneSaver;
    private readonly ImGuiController _controller;

    private bool _visible;
    private Entity? _selectedEntity;

    // Curated 3D component type ids handled by their own editor sections (see
    // ImGuiInspector.ComponentEditors3D.cs) rather than the generic read-only registry loop or the
    // "Add Component" combo's fallback entry (see ImGuiInspector.MainWindow.cs/AddComponent.cs).
    private static readonly string[] Curated3DTypeIds =
    [
        "Transform3D",
        "Camera3D",
        "MeshHandle",
        "Material3D",
        "DirectionalLight",
        "PointLight",
        "SpotLight",
    ];

    public ImGuiInspector(Window window, World world, ComponentRegistry? registry = null)
    {
        _world = world;
        _window = window;
        _registry = registry;
        _sceneSaver = registry is not null ? new SceneSaver(registry) : null;
        _controller = new ImGuiController(window.Gl, window.InnerView, window.InputContext);
        _gizmoRenderer = new GizmoRenderer(window.Gl);
    }

    /// <summary>Toggles the inspector overlay on or off.</summary>
    public void Toggle() => _visible = !_visible;

    /// <summary>
    /// Updates and renders the inspector overlay.  Call inside <c>OnRender</c> after all game
    /// rendering so the overlay sits on top.
    /// </summary>
    public void Render(double delta)
    {
        if (!_visible)
        {
            // Still flush any ops queued on the frame visibility was toggled off
            FlushPendingCommands();
            return;
        }

        _controller.Update((float)delta);
        DrawInspectorWindow();
        // Run after DrawInspectorWindow so ImGui.GetIO().WantCaptureMouse reflects this frame's
        // widget hover state, and before FlushPendingCommands so a drag queued this frame is applied
        // before gizmos are built (same reasoning as the comment below).
        UpdateViewportInteraction();
        // Apply queued edits now, before building gizmos, so the overlay reflects the value being
        // dragged this frame rather than lagging a frame behind. Safe here: all world-iterating
        // ImGui code (the entity list) ran in DrawInspectorWindow, and nothing below mutates or
        // iterates the world during draw — so this can't invalidate an in-flight iterator.
        FlushPendingCommands();
        // Draw gizmos into the scene framebuffer first so the ImGui panel renders on top of them.
        RenderGizmos();
        _controller.Render();
    }

    public void Dispose()
    {
        _gizmoRenderer.Dispose();
        _controller.Dispose();
    }
}

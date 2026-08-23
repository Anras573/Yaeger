using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;

namespace Yaeger.Inspector;

// World-space selection gizmos and viewport picking/translate-axis dragging. See
// ImGuiInspector.MainWindow.cs for the ImGui panel these gizmos overlay.
public sealed partial class ImGuiInspector
{
    // World-space overlay that visualises the selected entity (light direction/reach, mesh bounds,
    // camera frustum, transform axes). Driven from Render once the ImGui panel has updated selection.
    private readonly GizmoRenderer _gizmoRenderer;
    private readonly GizmoBuilder _gizmoBuilder = new();

    /// <summary>
    /// When true (the default), the selected entity is highlighted in the scene with gizmos.
    /// 3D entities are projected through the world's <see cref="Camera3D"/>; 2D entities
    /// (<see cref="Transform2D"/> / <see cref="Camera2D"/>) through the active <see cref="Camera2D"/>,
    /// falling back to NDC-direct when the 2D scene has no camera.
    /// </summary>
    public bool ShowGizmos { get; set; } = true;

    private GizmoStyle _gizmoStyle = new();

    /// <summary>
    /// Appearance of the selection gizmos — colours, sizes, segment counts, depth-test and line
    /// width. Defaults reproduce the engine's original look. Mutate the properties in place or assign
    /// a fresh <see cref="GizmoStyle"/> to retune the overlay (e.g. raise
    /// <see cref="GizmoStyle.SizeMultiplier"/> for a large-scale scene). Never <c>null</c> — the
    /// setter rejects it so <see cref="RenderGizmos"/> (which dereferences it every frame) can't NRE.
    /// </summary>
    public GizmoStyle GizmoStyle
    {
        get => _gizmoStyle;
        set => _gizmoStyle = value ?? throw new ArgumentNullException(nameof(value));
    }

    // ── Viewport picking + translate-axis dragging ────────────────────────────
    // Edge-detects the left mouse button by polling (mirroring UiSystem) rather than
    // Mouse.AddButtonDown/Up, which is a single-subscriber dictionary game code may also be using.
    private bool _leftMouseWasDown;

    // Set each frame by UpdateViewportInteraction while a handle is under the cursor (and not
    // dragging), so RenderGizmos can recolour it. Cleared whenever nothing is hovered.
    private int? _hoveredAxisIndex;

    private bool _isDraggingAxis;
    private Entity? _dragEntity;
    private int _dragAxisIndex;
    private bool _dragIs2D;
    private Vector3 _dragAxisOrigin;
    private Vector3 _dragAxisDirection;
    private float _dragStartParam;
    private Vector3 _dragStartPosition;

    // ── Selection gizmos (world-space overlay) ────────────────────────────────

    /// <summary>
    /// Draws the world-space gizmos for the current selection so the user can see what they are
    /// editing. Projects through the world's <see cref="Camera3D"/> for 3D entities and a
    /// <see cref="Camera2D"/>-derived (or identity) matrix for 2D entities; no-ops when it can't
    /// resolve a projection.
    /// </summary>
    private void RenderGizmos()
    {
        if (!ShowGizmos || !_selectedEntity.HasValue)
            return;

        var entity = _selectedEntity.Value;
        if (!_world.Entities.Contains(entity))
            return;

        if (!TryGetSceneViewProjection(entity, out var viewProj, out var aspectRatio))
            return;

        var highlightedAxis = _isDraggingAxis ? _dragAxisIndex : _hoveredAxisIndex;

        _gizmoBuilder.Clear();
        EntityGizmos.Build(_world, entity, aspectRatio, _gizmoBuilder, GizmoStyle, highlightedAxis);
        _gizmoRenderer.Render(
            _gizmoBuilder.Lines,
            viewProj,
            GizmoStyle.DepthTest,
            GizmoStyle.LineWidth
        );
    }

    // Resolves the view-projection (and aspect) used to draw gizmos for the selected entity.
    //
    // A selected 2D entity is projected through the 2D pipeline regardless of any 3D camera, since
    // the two live in different spaces: mirror RenderSystem — the first Camera2D wins, and a scene
    // with no Camera2D falls back to identity (NDC-direct), exactly as the 2D renderer does. So
    // even a camera-less Pong-style scene shows gizmos. Other entities project through the active
    // 3D camera (mirroring MeshRenderSystem); a 3D scene with no Camera3D shows nothing.
    private bool TryGetSceneViewProjection(
        Entity entity,
        out Matrix4x4 viewProj,
        out float aspectRatio
    )
    {
        var size = _window.Size;
        aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;

        if (IsTwoDimensional(entity))
        {
            foreach (var (_, camera) in _world.GetStore<Camera2D>().All())
            {
                viewProj = camera.ViewProjection(aspectRatio);
                return true;
            }

            viewProj = Matrix4x4.Identity;
            return true;
        }

        foreach (var (_, camera) in _world.GetStore<Camera3D>().All())
        {
            viewProj = camera.ViewMatrix * camera.ProjectionMatrix(aspectRatio);
            return true;
        }

        viewProj = Matrix4x4.Identity;
        return false;
    }

    private bool IsTwoDimensional(Entity entity) =>
        _world.TryGetComponent<Transform2D>(entity, out _)
        || _world.TryGetComponent<Camera2D>(entity, out _);

    // ── Viewport picking + translate-axis dragging ────────────────────────────

    /// <summary>
    /// Drives click-to-select viewport picking and translate-axis dragging. Left-button edges are
    /// detected by polling (mirroring <c>UiSystem</c>) rather than <c>Mouse.AddButtonDown/Up</c>,
    /// which is a single-subscriber dictionary game code may already be using for its own input.
    /// Mouse interaction never runs while ImGui wants the mouse (a widget is hovered/active) or while
    /// gizmos are hidden — there is nothing to click or drag in either case.
    /// </summary>
    private void UpdateViewportInteraction()
    {
        var isDown = Mouse.IsButtonPressed(MouseButton.Left);
        var justPressed = isDown && !_leftMouseWasDown;
        _leftMouseWasDown = isDown;

        if (_isDraggingAxis)
        {
            if (isDown)
                ContinueAxisDrag();
            else
                _isDraggingAxis = false;
            return;
        }

        _hoveredAxisIndex = null;

        if (!ShowGizmos || ImGui.GetIO().WantCaptureMouse)
            return;

        if (
            _selectedEntity is { } selected
            && _world.Entities.Contains(selected)
            && TryGetTranslateHandles(selected, out var handles, out var is2D)
            && TryGetSceneViewProjection(selected, out var viewProj, out _)
            && TryHitTestHandles(handles, viewProj, out var hitAxis)
        )
        {
            _hoveredAxisIndex = hitAxis;

            if (justPressed)
            {
                StartAxisDrag(selected, handles[hitAxis], hitAxis, is2D, viewProj);
                return;
            }
        }

        if (justPressed)
            _selectedEntity = PickEntityUnderCursor();
    }

    // Only entities carrying Transform2D or Transform3D expose translate handles — matches
    // EntityGizmos.Build, which only draws orientation axes for those two components.
    private bool TryGetTranslateHandles(Entity entity, out AxisHandle[] handles, out bool is2D)
    {
        var scale = EntityGizmos.EffectiveScale(GizmoStyle);

        if (_world.TryGetComponent<Transform2D>(entity, out var transform2D))
        {
            is2D = true;
            handles = TranslateHandles.For2D(
                transform2D.Position,
                transform2D.Rotation,
                GizmoStyle.Axis2DLength * scale
            );
            return handles.Length > 0;
        }

        if (_world.TryGetComponent<Transform3D>(entity, out var transform3D))
        {
            is2D = false;
            handles = TranslateHandles.For3D(
                transform3D.Position,
                transform3D.Rotation,
                GizmoStyle.AxisLength * scale
            );
            return handles.Length > 0;
        }

        is2D = false;
        handles = [];
        return false;
    }

    // Screen-space pixel-tolerance hit test so handles stay equally grabbable at any camera
    // zoom/distance; picks the nearest handle under the cursor within GizmoStyle.HandleHitTolerancePixels.
    private bool TryHitTestHandles(AxisHandle[] handles, Matrix4x4 viewProj, out int axisIndex)
    {
        axisIndex = -1;
        var mousePos = Mouse.Position;
        var windowSize = _window.Size;
        var bestDistance = float.MaxValue;

        foreach (var handle in handles)
        {
            var startScreen = ViewportPicking.WorldToScreen(handle.Origin, viewProj, windowSize);
            var endScreen = ViewportPicking.WorldToScreen(handle.End, viewProj, windowSize);
            var distance = ViewportPicking.DistancePointToSegment(mousePos, startScreen, endScreen);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                axisIndex = handle.AxisIndex;
            }
        }

        return axisIndex >= 0 && bestDistance <= GizmoStyle.HandleHitTolerancePixels;
    }

    private void StartAxisDrag(
        Entity entity,
        AxisHandle handle,
        int axisIndex,
        bool is2D,
        Matrix4x4 viewProj
    )
    {
        var direction = handle.Direction;
        if (direction.LengthSquared() < 1e-12f)
            return;
        direction = Vector3.Normalize(direction);

        float startParam;

        if (is2D)
        {
            if (!_world.TryGetComponent<Transform2D>(entity, out var transform2D))
                return;
            if (
                !ViewportPicking.TryUnprojectPoint2D(
                    Mouse.PositionNdc,
                    viewProj,
                    out var worldPoint
                )
            )
                return;

            startParam = ViewportPicking.ClosestPointOnAxis2D(
                new Vector2(handle.Origin.X, handle.Origin.Y),
                new Vector2(direction.X, direction.Y),
                worldPoint
            );
            _dragStartPosition = new Vector3(transform2D.Position, 0f);
        }
        else
        {
            if (!_world.TryGetComponent<Transform3D>(entity, out var transform3D))
                return;
            if (
                !ViewportPicking.TryGetPickRay(
                    Mouse.PositionNdc,
                    viewProj,
                    out var rayOrigin,
                    out var rayDir
                )
            )
                return;
            if (
                !ViewportPicking.TryClosestPointOnAxisToRay(
                    handle.Origin,
                    direction,
                    rayOrigin,
                    rayDir,
                    out startParam
                )
            )
                return;
            _dragStartPosition = transform3D.Position;
        }

        _isDraggingAxis = true;
        _dragEntity = entity;
        _dragAxisIndex = axisIndex;
        _dragIs2D = is2D;
        _dragAxisOrigin = handle.Origin;
        _dragAxisDirection = direction;
        _dragStartParam = startParam;
    }

    private void ContinueAxisDrag()
    {
        if (_dragEntity is not { } entity || !_world.Entities.Contains(entity))
        {
            _isDraggingAxis = false;
            return;
        }

        if (!TryGetSceneViewProjection(entity, out var viewProj, out _))
            return;

        float currentParam;

        if (_dragIs2D)
        {
            if (
                !ViewportPicking.TryUnprojectPoint2D(
                    Mouse.PositionNdc,
                    viewProj,
                    out var worldPoint
                )
            )
                return;

            currentParam = ViewportPicking.ClosestPointOnAxis2D(
                new Vector2(_dragAxisOrigin.X, _dragAxisOrigin.Y),
                new Vector2(_dragAxisDirection.X, _dragAxisDirection.Y),
                worldPoint
            );
        }
        else
        {
            if (
                !ViewportPicking.TryGetPickRay(
                    Mouse.PositionNdc,
                    viewProj,
                    out var rayOrigin,
                    out var rayDir
                )
            )
                return;
            if (
                !ViewportPicking.TryClosestPointOnAxisToRay(
                    _dragAxisOrigin,
                    _dragAxisDirection,
                    rayOrigin,
                    rayDir,
                    out currentParam
                )
            )
                return;
        }

        if (!float.IsFinite(currentParam))
            return;

        var newPosition =
            _dragStartPosition + _dragAxisDirection * (currentParam - _dragStartParam);

        if (_dragIs2D)
        {
            if (!_world.TryGetComponent<Transform2D>(entity, out var transform2D))
                return;
            transform2D.Position = new Vector2(newPosition.X, newPosition.Y);
            var snapshot = transform2D;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }
        else
        {
            if (!_world.TryGetComponent<Transform3D>(entity, out var transform3D))
                return;
            transform3D.Position = newPosition;
            var snapshot = transform3D;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }
    }

    /// <summary>
    /// Finds the nearest entity under the cursor: 3D meshes first (ray-vs-AABB through the first
    /// <see cref="Camera3D"/>, nearest hit wins — mirrors <c>MeshRenderSystem</c>'s camera choice),
    /// falling back to 2D sprite bounds (oriented-rect test through the first <see cref="Camera2D"/>
    /// or identity, mirroring the 2D render fallback and <see cref="TryGetSceneViewProjection"/>;
    /// smallest-area match wins so a small object stacked on a larger one is still selectable).
    /// Returns <c>null</c> when nothing is hit, which deselects.
    /// </summary>
    private Entity? PickEntityUnderCursor()
    {
        var ndc = Mouse.PositionNdc;
        var windowSize = _window.Size;
        var aspect = windowSize.Y > 0f ? windowSize.X / windowSize.Y : 1f;

        foreach (var (_, camera3D) in _world.GetStore<Camera3D>().All())
        {
            var viewProj = camera3D.ViewMatrix * camera3D.ProjectionMatrix(aspect);
            if (!ViewportPicking.TryGetPickRay(ndc, viewProj, out var rayOrigin, out var rayDir))
                break;

            Entity? bestMesh = null;
            var bestDistance = float.MaxValue;
            foreach (var (entity, transform, aabb) in _world.Query<Transform3D, Aabb3D>())
            {
                if (
                    ViewportPicking.TryIntersectRayAabb(
                        rayOrigin,
                        rayDir,
                        aabb,
                        transform.ModelMatrix,
                        out var distance
                    )
                    && distance < bestDistance
                )
                {
                    bestDistance = distance;
                    bestMesh = entity;
                }
            }

            if (bestMesh.HasValue)
                return bestMesh;

            break;
        }

        var view2D = Matrix4x4.Identity;
        foreach (var (_, camera2D) in _world.GetStore<Camera2D>().All())
        {
            view2D = camera2D.ViewProjection(aspect);
            break;
        }

        if (!ViewportPicking.TryUnprojectPoint2D(ndc, view2D, out var worldPoint))
            return null;

        Entity? bestSprite = null;
        var bestArea = float.MaxValue;
        foreach (var (entity, transform) in _world.GetStore<Transform2D>().All())
        {
            var halfExtents = transform.Scale * 0.5f;
            if (
                !ViewportPicking.PointInOrientedRect(
                    worldPoint,
                    transform.Position,
                    halfExtents,
                    transform.Rotation
                )
            )
                continue;

            var area = MathF.Abs(halfExtents.X * halfExtents.Y);
            if (area < bestArea)
            {
                bestArea = area;
                bestSprite = entity;
            }
        }

        return bestSprite;
    }
}

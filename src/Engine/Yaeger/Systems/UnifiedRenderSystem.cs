using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Renders sprites, sprite sheets, tilemaps, and text in a shared deterministic order using
/// <see cref="RenderLayer"/>, <see cref="Entity.Id"/>, and command kind as sort keys.
/// </summary>
public class UnifiedRenderSystem(
    IRenderSurface? renderer,
    ITextRenderSurface? textRenderer,
    World world,
    Window? window = null
)
{
    private readonly List<RenderCommand> _commands = Validate(renderer, textRenderer);

    // Active camera state captured during UpdateCamera, used for tilemap culling.
    private Camera2D? _activeCamera;
    private float _aspectRatio = 1f;

    private static List<RenderCommand> Validate(
        IRenderSurface? renderer,
        ITextRenderSurface? textRenderer
    )
    {
        if (renderer is null && textRenderer is null)
            throw new ArgumentException(
                "At least one of renderer or textRenderer must be non-null."
            );
        return [];
    }

    public void Render()
    {
        UpdateCamera();
        BuildCommandQueue();

        renderer?.BeginFrame();

        foreach (var command in _commands)
        {
            switch (command.Kind)
            {
                case RenderCommandKind.Sprite:
                    renderer!.SubmitQuad(command.Transform, command.TexturePath!, command.Color);
                    break;
                case RenderCommandKind.SpriteSheet:
                    renderer!.SubmitQuad(
                        command.Transform,
                        command.TexturePath!,
                        command.UvMin,
                        command.UvMax,
                        command.Color
                    );
                    break;
                case RenderCommandKind.Tilemap:
                    SubmitTilemap(command);
                    break;
                case RenderCommandKind.Text:
                    renderer?.FlushQueuedQuads();
                    if (command.TextComponent.TryGetNativeFont(out var nativeFont))
                    {
                        textRenderer!.DrawText(
                            command.TextComponent.Content,
                            command.Transform,
                            nativeFont,
                            command.TextComponent.FontSize,
                            command.TextComponent.Color
                        );
                    }
                    else
                    {
                        textRenderer!.DrawText(
                            command.TextComponent.Content,
                            command.Transform,
                            command.TextComponent.FontHandle,
                            command.TextComponent.FontSize,
                            command.TextComponent.Color
                        );
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown render command kind: {command.Kind}"
                    );
            }
        }

        renderer?.EndFrame();
    }

    private void BuildCommandQueue()
    {
        _commands.Clear();

        if (renderer != null)
        {
            var spriteSheetStore = world.GetStore<SpriteSheet>();
            var animationStateStore = world.GetStore<AnimationState>();
            var renderLayerStore = world.GetStore<RenderLayer>();

            foreach (
                (Entity entity, Sprite sprite, Transform2D transform) in world.Query<
                    Sprite,
                    Transform2D
                >()
            )
            {
                if (
                    spriteSheetStore.TryGet(entity, out _)
                    && animationStateStore.TryGet(entity, out _)
                )
                    continue;

                _commands.Add(
                    RenderCommand.ForSprite(
                        entity,
                        GetRenderLayerValue(renderLayerStore, entity),
                        transform.TransformMatrix,
                        sprite
                    )
                );
            }

            foreach (
                (
                    Entity entity,
                    SpriteSheet sheet,
                    AnimationState state,
                    Transform2D transform
                ) in world.Query<SpriteSheet, AnimationState, Transform2D>()
            )
            {
                if (sheet.FrameCount <= 0)
                    continue;

                var frameIndex = Math.Clamp(state.CurrentFrameIndex, 0, sheet.FrameCount - 1);
                var (uvMin, uvMax) = sheet.GetFrameUv(frameIndex);

                _commands.Add(
                    RenderCommand.ForSpriteSheet(
                        entity,
                        GetRenderLayerValue(renderLayerStore, entity),
                        transform.TransformMatrix,
                        sheet.TexturePath,
                        uvMin,
                        uvMax,
                        sheet.Tint.ToVector4()
                    )
                );
            }

            // One command per tilemap; individual tile quads are expanded at execution time
            // so the per-frame sort stays proportional to entity count, not tile count.
            foreach (
                (Entity entity, Tilemap tilemap, Transform2D transform) in world.Query<
                    Tilemap,
                    Transform2D
                >()
            )
            {
                if (tilemap.Tiles is null || tilemap.Tileset.TileCount <= 0)
                    continue;

                _commands.Add(
                    RenderCommand.ForTilemap(
                        entity,
                        GetRenderLayerValue(renderLayerStore, entity),
                        transform,
                        tilemap
                    )
                );
            }
        }

        if (textRenderer != null)
        {
            var renderLayerStore = world.GetStore<RenderLayer>();

            foreach (
                (Entity entity, Text text, Transform2D transform) in world.Query<
                    Text,
                    Transform2D
                >()
            )
            {
                _commands.Add(
                    RenderCommand.ForText(
                        entity,
                        GetRenderLayerValue(renderLayerStore, entity),
                        transform.TransformMatrix,
                        text
                    )
                );
            }
        }

        _commands.Sort(
            static (a, b) =>
            {
                var layerComparison = a.Layer.CompareTo(b.Layer);
                if (layerComparison != 0)
                    return layerComparison;

                var entityComparison = a.Entity.Id.CompareTo(b.Entity.Id);
                if (entityComparison != 0)
                    return entityComparison;

                return a.Kind.CompareTo(b.Kind);
            }
        );
    }

    private static int GetRenderLayerValue(
        ComponentStorage<RenderLayer> renderLayerStore,
        Entity entity
    ) => renderLayerStore.TryGet(entity, out var renderLayer) ? renderLayer.Value : 0;

    private void UpdateCamera()
    {
        _activeCamera = null;

        if (renderer is null || window is null)
            return;

        foreach (var (_, camera) in world.GetStore<Camera2D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0 ? size.X / size.Y : 1f;
            _activeCamera = camera;
            _aspectRatio = aspectRatio;
            renderer.SetCamera(camera.ViewProjection(aspectRatio));
            return;
        }

        renderer.SetCamera(Matrix4x4.Identity);
    }

    private void SubmitTilemap(in RenderCommand command)
    {
        var map = command.TilemapComponent;
        var mapTransform = command.SourceTransform;
        var tint = map.Tint.ToVector4();

        var (columnMin, columnMax, rowMin, rowMax) = _activeCamera is { } camera
            ? GetVisibleTileRange(map, mapTransform, camera, _aspectRatio)
            : (0, map.Width - 1, 0, map.Height - 1);

        for (var row = rowMin; row <= rowMax; row++)
        {
            for (var column = columnMin; column <= columnMax; column++)
            {
                var tileIndex = map.Tiles[row * map.Width + column];
                if (tileIndex == Tilemap.EmptyTile)
                    continue;

                var (uvMin, uvMax) = map.Tileset.GetTileUv(tileIndex);

                // Tile quad in map-local space: the unit quad is centred on the origin, so
                // scale it to tile size and translate to the cell centre (row 0 is the top
                // row, hence the Height - 1 - row flip into the Y-up world).
                var local =
                    Matrix4x4.CreateScale(map.TileSize.X, map.TileSize.Y, 1f)
                    * Matrix4x4.CreateTranslation(
                        (column + 0.5f) * map.TileSize.X,
                        (map.Height - 1 - row + 0.5f) * map.TileSize.Y,
                        0f
                    );

                renderer!.SubmitQuad(
                    local * command.Transform,
                    map.Tileset.TexturePath,
                    uvMin,
                    uvMax,
                    tint
                );
            }
        }
    }

    /// <summary>
    /// Computes the inclusive tile-cell range of <paramref name="map"/> that can intersect the
    /// camera's visible span, in (columnMin, columnMax, rowMin, rowMax) form. Falls back to the
    /// full map when the map transform is rotated or non-positively scaled, or the tile size is
    /// non-positive (conservative: never culls a visible tile). Returns an empty range
    /// (min &gt; max) when the map is fully off-screen.
    /// </summary>
    internal static (int ColumnMin, int ColumnMax, int RowMin, int RowMax) GetVisibleTileRange(
        Tilemap map,
        Transform2D mapTransform,
        Camera2D camera,
        float aspectRatio
    )
    {
        // Culling maps the camera's world-space AABB into tile cells, which is only valid for
        // an axis-aligned, positively scaled map with a positive tile size (TileSize is a
        // mutable field, so it can be zeroed after construction). Anything else renders the
        // full map.
        if (
            mapTransform.Rotation != 0f
            || mapTransform.Scale.X <= 0f
            || mapTransform.Scale.Y <= 0f
            || map.TileSize.X <= 0f
            || map.TileSize.Y <= 0f
        )
            return (0, map.Width - 1, 0, map.Height - 1);

        // Visible world region: a rect centred on the camera with half-extents
        // (aspect / zoom, 1 / zoom), rotated by the camera rotation. Use its AABB.
        var zoom = camera.Zoom > 0f ? camera.Zoom : 1f;
        var halfWidth = aspectRatio / zoom;
        var halfHeight = 1f / zoom;
        var cos = MathF.Abs(MathF.Cos(camera.Rotation));
        var sin = MathF.Abs(MathF.Sin(camera.Rotation));
        var extentX = cos * halfWidth + sin * halfHeight;
        var extentY = sin * halfWidth + cos * halfHeight;

        // View AABB in map-local units (map origin at the bottom-left corner).
        var localMinX =
            (camera.Position.X - extentX - mapTransform.Position.X) / mapTransform.Scale.X;
        var localMaxX =
            (camera.Position.X + extentX - mapTransform.Position.X) / mapTransform.Scale.X;
        var localMinY =
            (camera.Position.Y - extentY - mapTransform.Position.Y) / mapTransform.Scale.Y;
        var localMaxY =
            (camera.Position.Y + extentY - mapTransform.Position.Y) / mapTransform.Scale.Y;

        var columnMin = Math.Max((int)MathF.Floor(localMinX / map.TileSize.X), 0);
        var columnMax = Math.Min((int)MathF.Ceiling(localMaxX / map.TileSize.X) - 1, map.Width - 1);

        // Row 0 is the top row: local Y grows towards row 0.
        var rowMin = Math.Max(map.Height - (int)MathF.Ceiling(localMaxY / map.TileSize.Y), 0);
        var rowMax = Math.Min(
            map.Height - 1 - (int)MathF.Floor(localMinY / map.TileSize.Y),
            map.Height - 1
        );

        return (columnMin, columnMax, rowMin, rowMax);
    }

    // Text must stay the last kind: it is the tie-break for commands on the same entity and
    // layer, and text execution flushes queued quads before drawing — any quad kind sorted
    // after it would paint over the text.
    private enum RenderCommandKind
    {
        Sprite = 0,
        SpriteSheet = 1,
        Tilemap = 2,
        Text = 3,
    }

    private readonly record struct RenderCommand(
        Entity Entity,
        int Layer,
        RenderCommandKind Kind,
        Matrix4x4 Transform,
        string? TexturePath,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color,
        Text TextComponent,
        Tilemap TilemapComponent,
        Transform2D SourceTransform
    )
    {
        public static RenderCommand ForSprite(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            Sprite sprite
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.Sprite,
                transform,
                sprite.TexturePath,
                Vector2.Zero,
                Vector2.One,
                sprite.Tint.ToVector4(),
                default,
                default,
                default
            );

        public static RenderCommand ForSpriteSheet(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.SpriteSheet,
                transform,
                texturePath,
                uvMin,
                uvMax,
                color,
                default,
                default,
                default
            );

        public static RenderCommand ForText(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            Text text
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.Text,
                transform,
                null,
                Vector2.Zero,
                Vector2.One,
                Vector4.Zero,
                text,
                default,
                default
            );

        public static RenderCommand ForTilemap(
            Entity entity,
            int layer,
            Transform2D transform,
            Tilemap tilemap
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.Tilemap,
                transform.TransformMatrix,
                null,
                Vector2.Zero,
                Vector2.One,
                Vector4.Zero,
                default,
                tilemap,
                transform
            );
    }
}

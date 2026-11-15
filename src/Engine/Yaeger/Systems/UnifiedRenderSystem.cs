using System.Numerics;
using System.Runtime.InteropServices;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Systems;

/// <summary>
/// Renders sprites, sprite sheets, tilemaps, and text in a shared deterministic order using
/// <see cref="RenderLayer"/>, <see cref="Entity.Id"/>, and command kind as sort keys.
/// </summary>
/// <remarks>
/// Sprite-sheet and tile UVs are inset by half a texel to avoid neighbour bleed, which needs
/// each texture's pixel size. By default it comes from
/// <see cref="IRenderSurface.GetTextureSize"/>; pass <c>textureSizeProvider</c> to supply it
/// explicitly (e.g. when the surface is a wrapper that does not forward that member). A
/// provider returning <see cref="Vector2.Zero"/> means "unknown" and disables the inset.
/// </remarks>
public class UnifiedRenderSystem(
    IRenderSurface? renderer,
    ITextRenderSurface? textRenderer,
    World world,
    IViewport? viewport = null,
    Func<string, Vector2>? textureSizeProvider = null
)
{
    // Commands are split by kind into compact payload lists; the sort runs over a parallel
    // (key, payload index) pair of lists instead of over fat structs with a delegate.
    // Key layout (high to low): biased layer | biased entity id | kind (2 bits), so an
    // unsigned compare reproduces layer, then entity id, then kind exactly.
    private readonly List<UInt128> _keys = Validate(renderer, textRenderer);
    private readonly List<int> _payloadIndices = [];
    private readonly List<QuadCommand> _quadCommands = [];
    private readonly List<TilemapCommand> _tilemapCommands = [];
    private readonly List<TextCommand> _textCommands = [];

    // Per-tilemap tile UV cache, keyed by entity. See TileUvCache.
    private readonly Dictionary<Entity, TileUvCache> _tileUvCaches = [];
    private readonly List<Entity> _staleCacheScratch = [];
    private int _frameNumber;

    // Last texture-size lookup, so a run of sprite-sheet entities sharing one texture does one
    // string-keyed lookup instead of one each.
    private string? _lastSizePath;
    private Vector2 _lastSize;

    // Active camera state captured during UpdateCamera, used for tilemap culling.
    private Camera2D? _activeCamera;
    private float _aspectRatio = 1f;

    private static List<UInt128> Validate(
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

    private Vector2 GetTextureSize(string path)
    {
        if (ReferenceEquals(path, _lastSizePath) && _lastSize != Vector2.Zero)
            return _lastSize;

        var size =
            textureSizeProvider is not null ? textureSizeProvider(path)
            : renderer is not null ? renderer.GetTextureSize(path)
            : default;

        // Only remember a known size: an unknown one (browser: texture still loading) must be
        // re-queried so the half-texel inset kicks in once the texture arrives.
        _lastSizePath = path;
        _lastSize = size;
        return size;
    }

    /// <summary>Renders the frame, clearing the target first.</summary>
    public void Render() => Render(clear: true);

    /// <summary>
    /// Renders the frame. Pass <c>clear: false</c> to draw on top of what an earlier pass left
    /// in the target (an overlay or HUD pass) instead of clearing it first.
    /// </summary>
    public void Render(bool clear)
    {
        _frameNumber++;
        _lastSizePath = null; // sizes can change between frames (hot reload, async load)
        UpdateCamera();
        BuildCommandQueue();

        renderer?.BeginFrame(clear);

        var screenSpace = false;
        for (var i = 0; i < _keys.Count; i++)
        {
            var kind = (RenderCommandKind)(int)(ulong)(_keys[i] & KindMask);
            var payload = _payloadIndices[i];

            // Text is screen-space on every runtime: native TextRenderer has no camera, so the
            // browser (which draws glyphs as quads through the camera-aware surface) must
            // match. Switching the camera flushes the quads queued under the previous one.
            var wantScreenSpace = kind == RenderCommandKind.Text;
            if (wantScreenSpace != screenSpace)
            {
                SetTextSpace(wantScreenSpace);
                screenSpace = wantScreenSpace;
            }

            switch (kind)
            {
                case RenderCommandKind.Sprite:
                case RenderCommandKind.SpriteSheet:
                    var quad = CollectionsMarshal.AsSpan(_quadCommands)[payload];
                    renderer!.SubmitQuad(
                        quad.Transform,
                        quad.TexturePath,
                        quad.UvMin,
                        quad.UvMax,
                        quad.Color
                    );
                    break;
                case RenderCommandKind.Tilemap:
                    SubmitTilemap(in CollectionsMarshal.AsSpan(_tilemapCommands)[payload]);
                    break;
                case RenderCommandKind.Text:
                    renderer?.FlushQueuedQuads();
                    ref readonly var text = ref CollectionsMarshal.AsSpan(_textCommands)[payload];
                    textRenderer!.DrawText(
                        text.Component.Content,
                        TextTransform(in text),
                        text.Component.FontObject ?? (IFontHandle)text.Component.FontHandle,
                        text.Component.FontSize,
                        text.Component.Color
                    );
                    break;
                default:
                    throw new InvalidOperationException($"Unknown render command kind: {kind}");
            }
        }

        if (screenSpace)
            SetTextSpace(false);

        renderer?.EndFrame();
        PruneTileUvCaches();
    }

    // Text always draws under an identity surface camera (native TextRenderer has none), so a
    // world-space label bakes the camera into its transform instead. Both runtimes then agree.
    private Matrix4x4 TextTransform(in TextCommand command) =>
        command.Component.WorldSpace && _activeCamera is { } camera
            ? command.Transform * camera.ViewProjection(_aspectRatio)
            : command.Transform;

    private void SetTextSpace(bool screenSpace)
    {
        if (renderer is null || _activeCamera is not { } camera)
            return;

        renderer.SetCamera(screenSpace ? Matrix4x4.Identity : camera.ViewProjection(_aspectRatio));
    }

    private void BuildCommandQueue()
    {
        _keys.Clear();
        _payloadIndices.Clear();
        _quadCommands.Clear();
        _tilemapCommands.Clear();
        _textCommands.Clear();

        if (renderer != null)
        {
            var spriteStore = world.GetStore<Sprite>();
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

                var (flippedMin, flippedMax) = Sprite.ApplyFlip(
                    Vector2.Zero,
                    Vector2.One,
                    sprite.FlipX,
                    sprite.FlipY
                );
                AddQuad(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    RenderCommandKind.Sprite,
                    new QuadCommand(
                        transform.TransformMatrix,
                        sprite.TexturePath,
                        flippedMin,
                        flippedMax,
                        sprite.Tint.ToVector4()
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
                var (uvMin, uvMax) = sheet.GetFrameUv(
                    frameIndex,
                    GetTextureSize(sheet.TexturePath)
                );

                // An optional co-located Sprite carries facing/flip state for an animated
                // entity — its own TexturePath is unused here (SpriteSheet's is authoritative),
                // only FlipX/FlipY apply.
                if (spriteStore.TryGet(entity, out var spriteForFlip))
                    (uvMin, uvMax) = Sprite.ApplyFlip(
                        uvMin,
                        uvMax,
                        spriteForFlip.FlipX,
                        spriteForFlip.FlipY
                    );

                AddQuad(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    RenderCommandKind.SpriteSheet,
                    new QuadCommand(
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

                AddKey(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    RenderCommandKind.Tilemap,
                    _tilemapCommands.Count
                );
                _tilemapCommands.Add(new TilemapCommand(entity, transform, tilemap));
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
                AddKey(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    RenderCommandKind.Text,
                    _textCommands.Count
                );
                _textCommands.Add(new TextCommand(transform.TransformMatrix, text));
            }
        }

        // Unstable sort is fine: keys are unique (an entity has at most one command per kind).
        MemoryExtensions.Sort(
            CollectionsMarshal.AsSpan(_keys),
            CollectionsMarshal.AsSpan(_payloadIndices)
        );
    }

    private void AddQuad(Entity entity, int layer, RenderCommandKind kind, in QuadCommand command)
    {
        AddKey(entity, layer, kind, _quadCommands.Count);
        _quadCommands.Add(command);
    }

    private void AddKey(Entity entity, int layer, RenderCommandKind kind, int payloadIndex)
    {
        // XOR with int.MinValue maps signed order onto unsigned order.
        var high = (ulong)(uint)(layer ^ int.MinValue);
        var low = ((ulong)(uint)(entity.Id ^ int.MinValue) << 2) | (ulong)kind;
        _keys.Add(new UInt128(high, low));
        _payloadIndices.Add(payloadIndex);
    }

    private static int GetRenderLayerValue(
        ComponentStorage<RenderLayer> renderLayerStore,
        Entity entity
    ) => renderLayerStore.TryGet(entity, out var renderLayer) ? renderLayer.Value : 0;

    private void UpdateCamera()
    {
        _activeCamera = null;

        if (renderer is null || viewport is null)
            return;

        foreach (var (_, camera) in world.GetStore<Camera2D>().All())
        {
            var aspectRatio = viewport.AspectRatio;
            _activeCamera = camera;
            _aspectRatio = aspectRatio;
            renderer.SetCamera(camera.ViewProjection(aspectRatio));
            return;
        }

        renderer.SetCamera(Matrix4x4.Identity);
    }

    private void SubmitTilemap(in TilemapCommand command)
    {
        var map = command.Tilemap;
        var mapTransform = command.Transform;
        var tint = map.Tint.ToVector4();

        var (columnMin, columnMax, rowMin, rowMax) = _activeCamera is { } camera
            ? GetVisibleTileRange(map, mapTransform, camera, _aspectRatio)
            : (0, map.Width - 1, 0, map.Height - 1);

        // Fully off-screen map: skip without scanning rows.
        if (columnMin > columnMax || rowMin > rowMax)
            return;

        var cache = GetTileUvCache(command.Entity, map);
        var texturePath = map.Tileset.TexturePath;

        // The map's own placement is one affine transform; a tile is that transform applied to
        // an axis-aligned cell, so per tile we only step along the map's basis vectors instead
        // of building and multiplying matrices. matrix rows: M1x = image of +X, M2x = image of +Y.
        var matrix = mapTransform.TransformMatrix;
        var stepX = new Vector2(matrix.M11, matrix.M12) * map.TileSize.X; // one tile right
        var stepY = new Vector2(matrix.M21, matrix.M22) * map.TileSize.Y; // one tile up
        var origin = new Vector2(matrix.M41, matrix.M42);

        var tiles = map.Tiles.AsSpan();
        for (var row = rowMin; row <= rowMax; row++)
        {
            // Cell centre = (column + 0.5, (Height - 1 - row) + 0.5) tiles from the map origin
            // (row 0 is the top row, hence the flip into the Y-up world).
            var rowOrigin = origin + stepY * (map.Height - 1 - row + 0.5f);
            for (var column = columnMin; column <= columnMax; column++)
            {
                var cell = row * map.Width + column;
                if (tiles[cell] == Tilemap.EmptyTile)
                    continue;

                renderer!.SubmitQuad(
                    rowOrigin + stepX * (column + 0.5f),
                    stepX,
                    stepY,
                    texturePath,
                    cache.UvMin[cell],
                    cache.UvMax[cell],
                    tint
                );
            }
        }
    }

    /// <summary>
    /// Returns the UV cache for a tilemap, creating it or patching it to match the current
    /// tiles. A static map costs one vectorised compare of the tile array per frame; an edit
    /// re-resolves only the cells that changed. A change to the tileset, or to the texture's
    /// size (async load, hot reload), rebuilds the whole cache.
    /// </summary>
    private TileUvCache GetTileUvCache(Entity entity, in Tilemap map)
    {
        var textureSize = GetTextureSize(map.Tileset.TexturePath);

        if (
            !_tileUvCaches.TryGetValue(entity, out var cache)
            || !cache.Matches(map.Tileset, textureSize, map.Tiles.Length)
        )
        {
            cache = new TileUvCache(map.Tileset, textureSize, map.Tiles.Length);
            _tileUvCaches[entity] = cache;
        }

        cache.LastUsedFrame = _frameNumber;
        cache.Sync(map.Tiles);
        return cache;
    }

    private void PruneTileUvCaches()
    {
        if (_tileUvCaches.Count == 0)
            return;

        _staleCacheScratch.Clear();
        foreach (var (entity, cache) in _tileUvCaches)
        {
            if (cache.LastUsedFrame != _frameNumber)
                _staleCacheScratch.Add(entity);
        }

        foreach (var entity in _staleCacheScratch)
            _tileUvCaches.Remove(entity);
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
        // !(x > 0) rather than x <= 0 so NaN also falls back (NaN comparisons are false).
        if (
            mapTransform.Rotation != 0f
            || !(mapTransform.Scale.X > 0f)
            || !(mapTransform.Scale.Y > 0f)
            || !float.IsFinite(map.TileSize.X)
            || !float.IsFinite(map.TileSize.Y)
            || !(map.TileSize.X > 0f)
            || !(map.TileSize.Y > 0f)
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

    // Low bits of a sort key hold the kind.
    private static readonly UInt128 KindMask = 3;

    private readonly record struct QuadCommand(
        Matrix4x4 Transform,
        string TexturePath,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color
    );

    private readonly record struct TilemapCommand(
        Entity Entity,
        Transform2D Transform,
        Tilemap Tilemap
    );

    private readonly record struct TextCommand(Matrix4x4 Transform, Text Component);

    /// <summary>
    /// Cached per-cell UV rectangles for one tilemap (the expensive per-tile work that never
    /// changes while the tiles don't). Holds a snapshot of the tile indices it was built from,
    /// so it detects edits made through <c>SetTile</c> or by writing <c>Tiles</c> directly.
    /// </summary>
    private sealed class TileUvCache
    {
        private readonly Tileset _tileset;
        private readonly Vector2 _textureSize;
        private readonly int[] _snapshot;

        public readonly Vector2[] UvMin;
        public readonly Vector2[] UvMax;
        public int LastUsedFrame;
        private bool _filled;

        public TileUvCache(Tileset tileset, Vector2 textureSize, int cellCount)
        {
            _tileset = tileset;
            _textureSize = textureSize;
            _snapshot = new int[cellCount];
            UvMin = new Vector2[cellCount];
            UvMax = new Vector2[cellCount];
        }

        public bool Matches(Tileset tileset, Vector2 textureSize, int cellCount) =>
            _snapshot.Length == cellCount
            && _textureSize == textureSize
            && _tileset.Columns == tileset.Columns
            && _tileset.Rows == tileset.Rows
            && string.Equals(_tileset.TexturePath, tileset.TexturePath, StringComparison.Ordinal);

        public void Sync(int[] tiles)
        {
            if (_filled && tiles.AsSpan().SequenceEqual(_snapshot))
                return;

            for (var i = 0; i < tiles.Length; i++)
            {
                if (_filled && tiles[i] == _snapshot[i])
                    continue;

                _snapshot[i] = tiles[i];
                if (tiles[i] != Tilemap.EmptyTile)
                    (UvMin[i], UvMax[i]) = _tileset.GetTileUv(tiles[i], _textureSize);
            }

            _filled = true;
        }
    }
}

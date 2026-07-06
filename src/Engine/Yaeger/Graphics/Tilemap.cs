using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Component that describes a rectangular grid of tiles referencing a <see cref="Tileset"/>.
/// Pair with a <see cref="Transform2D"/>, whose position is the <b>bottom-left corner</b>
/// of the map in world space.
/// </summary>
/// <remarks>
/// <para>
/// Tiles are stored row-major with row 0 at the <b>top</b> of the map (matching how tile
/// grids read when authored as text or exported from editors such as Tiled). During
/// rendering, rows are mapped to a Y-up world: tile (column, row) occupies the world-space
/// cell whose bottom-left corner is
/// <c>(column * TileSize.X, (Height - 1 - row) * TileSize.Y)</c> relative to the entity's
/// position.
/// </para>
/// <para>
/// A cell holding <see cref="EmptyTile"/> renders nothing. All tiles share the tileset
/// texture, so the batched renderer draws a tilemap in as few draw calls as its batch
/// capacity allows — typically one per map (it flushes every 1 000 quads, so very large
/// visible regions may span multiple batches). Use multiple tilemap entities with
/// different <see cref="RenderLayer"/> values for background/foreground layering.
/// </para>
/// </remarks>
public struct Tilemap
{
    /// <summary>Sentinel tile index for an empty cell (renders nothing).</summary>
    public const int EmptyTile = -1;

    /// <summary>Gets the tileset providing the texture and per-tile UV rectangles.</summary>
    public Tileset Tileset { get; }

    /// <summary>Gets the width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>
    /// Gets the tile indices, row-major with row 0 at the top of the map.
    /// Prefer <see cref="GetTile"/> / <see cref="SetTile"/> for validated access.
    /// </summary>
    public int[] Tiles { get; }

    /// <summary>Size of one tile in world units.</summary>
    public Vector2 TileSize;

    /// <summary>Tint colour applied when rendering this map. Defaults to white (no tint).</summary>
    public Color Tint;

    /// <summary>
    /// Initializes a new <see cref="Tilemap"/> with every cell set to <see cref="EmptyTile"/>.
    /// </summary>
    /// <param name="tileset">The tileset providing texture and tile UVs. Must contain at least one tile.</param>
    /// <param name="width">Width of the map in tiles. Must be at least 1.</param>
    /// <param name="height">Height of the map in tiles. Must be at least 1.</param>
    /// <param name="tileSize">
    /// Size of one tile in world units. Both components must be positive finite numbers.
    /// Defaults to (1, 1).
    /// </param>
    /// <param name="tint">Tint colour applied during rendering. Defaults to <see cref="Color.White"/>.</param>
    public Tilemap(
        Tileset tileset,
        int width,
        int height,
        Vector2? tileSize = null,
        Color? tint = null
    )
    {
        ValidateDimensions(tileset, width, height, tileSize);

        Tileset = tileset;
        Width = width;
        Height = height;
        TileSize = tileSize ?? Vector2.One;
        Tint = tint ?? Color.White;
        Tiles = new int[width * height];
        Array.Fill(Tiles, EmptyTile);
    }

    /// <summary>
    /// Initializes a new <see cref="Tilemap"/> from an existing tile array.
    /// </summary>
    /// <param name="tileset">The tileset providing texture and tile UVs. Must contain at least one tile.</param>
    /// <param name="width">Width of the map in tiles. Must be at least 1.</param>
    /// <param name="height">Height of the map in tiles. Must be at least 1.</param>
    /// <param name="tiles">
    /// Tile indices, row-major with row 0 at the top. Length must equal
    /// <paramref name="width"/> × <paramref name="height"/>, and every index must be
    /// <see cref="EmptyTile"/> or within [0, <see cref="Yaeger.Graphics.Tileset.TileCount"/>).
    /// The array is used directly (not copied).
    /// </param>
    /// <param name="tileSize">
    /// Size of one tile in world units. Both components must be positive finite numbers.
    /// Defaults to (1, 1).
    /// </param>
    /// <param name="tint">Tint colour applied during rendering. Defaults to <see cref="Color.White"/>.</param>
    public Tilemap(
        Tileset tileset,
        int width,
        int height,
        int[] tiles,
        Vector2? tileSize = null,
        Color? tint = null
    )
    {
        ValidateDimensions(tileset, width, height, tileSize);
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Length != width * height)
            throw new ArgumentException(
                $"Tile array length ({tiles.Length}) must equal width * height ({width * height}).",
                nameof(tiles)
            );

        for (var i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] < EmptyTile || tiles[i] >= tileset.TileCount)
                throw new ArgumentOutOfRangeException(
                    nameof(tiles),
                    tiles[i],
                    $"Tile index at position {i} must be {EmptyTile} (empty) or within [0, {tileset.TileCount})."
                );
        }

        Tileset = tileset;
        Width = width;
        Height = height;
        TileSize = tileSize ?? Vector2.One;
        Tint = tint ?? Color.White;
        Tiles = tiles;
    }

    /// <summary>
    /// Gets the tile index at the given cell.
    /// </summary>
    /// <param name="column">Zero-based column from the left edge.</param>
    /// <param name="row">Zero-based row from the <b>top</b> edge.</param>
    /// <returns>The tile index, or <see cref="EmptyTile"/> for an empty cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is outside the map.</exception>
    public readonly int GetTile(int column, int row)
    {
        ValidateCell(column, row);
        return Tiles[row * Width + column];
    }

    /// <summary>
    /// Sets the tile index at the given cell. The backing array is shared between copies
    /// of this component, so the change is visible without re-adding the component.
    /// </summary>
    /// <param name="column">Zero-based column from the left edge.</param>
    /// <param name="row">Zero-based row from the <b>top</b> edge.</param>
    /// <param name="tileIndex">
    /// The new tile index: <see cref="EmptyTile"/> or within [0, <see cref="Yaeger.Graphics.Tileset.TileCount"/>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the cell is outside the map or the tile index is invalid.
    /// </exception>
    public readonly void SetTile(int column, int row, int tileIndex)
    {
        ValidateCell(column, row);

        if (tileIndex < EmptyTile || tileIndex >= Tileset.TileCount)
            throw new ArgumentOutOfRangeException(
                nameof(tileIndex),
                tileIndex,
                $"Tile index must be {EmptyTile} (empty) or within [0, {Tileset.TileCount})."
            );

        Tiles[row * Width + column] = tileIndex;
    }

    private static void ValidateDimensions(
        Tileset tileset,
        int width,
        int height,
        Vector2? tileSize
    )
    {
        if (tileset.TileCount < 1)
            throw new ArgumentException(
                "Tileset must contain at least one tile. Was it default-constructed?",
                nameof(tileset)
            );

        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        // !(x > 0) rather than x <= 0 so NaN is rejected too (NaN comparisons are false).
        if (
            tileSize is { } size
            && (
                !(size.X > 0) || !(size.Y > 0) || !float.IsFinite(size.X) || !float.IsFinite(size.Y)
            )
        )
            throw new ArgumentOutOfRangeException(
                nameof(tileSize),
                size,
                "Tile size components must be positive finite numbers."
            );
    }

    private readonly void ValidateCell(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Height);
    }
}

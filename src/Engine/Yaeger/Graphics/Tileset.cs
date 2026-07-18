using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Describes a tileset texture divided into a uniform grid of equally-sized tiles.
/// Tiles are indexed left-to-right, top-to-bottom starting at 0, using the same
/// UV math as <see cref="SpriteSheet"/>.
/// </summary>
public readonly struct Tileset
{
    private readonly SpriteSheet _sheet;
    private readonly bool[]? _solid;

    /// <summary>
    /// Initializes a new <see cref="Tileset"/>.
    /// </summary>
    /// <param name="texturePath">Path to the tileset image file.</param>
    /// <param name="columns">Number of equally-wide columns in the tileset.</param>
    /// <param name="rows">Number of equally-tall rows in the tileset. Defaults to 1.</param>
    /// <param name="solidTileIndices">
    /// Zero-based tile indices that are solid for collision purposes (see
    /// <see cref="IsSolid"/>). Tiles not listed here are non-solid. Defaults to no solid
    /// tiles.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="columns"/> × <paramref name="rows"/> exceeds
    /// <see cref="int.MaxValue"/> (SpriteSheet computes the product unchecked, so it would
    /// silently wrap to a wrong tile count otherwise), or when a solid tile index falls
    /// outside [0, tile count).
    /// </exception>
    public Tileset(
        string texturePath,
        int columns,
        int rows = 1,
        IEnumerable<int>? solidTileIndices = null
    )
    {
        if (columns > 0 && rows > 0 && (long)columns * rows > int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                $"'columns' ({columns}) x 'rows' ({rows}) must not exceed {int.MaxValue}."
            );

        _sheet = new SpriteSheet(texturePath, columns, rows);

        if (solidTileIndices is not null)
        {
            var solid = new bool[_sheet.FrameCount];
            foreach (var tileIndex in solidTileIndices)
            {
                if (tileIndex < 0 || tileIndex >= solid.Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(solidTileIndices),
                        tileIndex,
                        $"Solid tile index must be within [0, {solid.Length})."
                    );
                solid[tileIndex] = true;
            }
            _solid = solid;
        }
    }

    /// <summary>Gets the path to the tileset texture.</summary>
    public string TexturePath => _sheet.TexturePath;

    /// <summary>Gets the number of columns in the tileset.</summary>
    public int Columns => _sheet.Columns;

    /// <summary>Gets the number of rows in the tileset.</summary>
    public int Rows => _sheet.Rows;

    /// <summary>
    /// Gets the total number of tiles (<see cref="Columns"/> × <see cref="Rows"/>).
    /// A default-constructed tileset has a tile count of 0.
    /// </summary>
    public int TileCount => _sheet.FrameCount;

    /// <summary>
    /// Returns the normalised UV rectangle for the given zero-based tile index.
    /// </summary>
    /// <param name="tileIndex">Zero-based tile index (left-to-right, top-to-bottom).</param>
    /// <returns>
    /// A tuple of (<c>uvMin</c>, <c>uvMax</c>) where both are normalised [0, 1] coordinates.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="tileIndex"/> is outside [0, <see cref="TileCount"/>).
    /// </exception>
    public (Vector2 UvMin, Vector2 UvMax) GetTileUv(int tileIndex) => _sheet.GetFrameUv(tileIndex);

    /// <summary>
    /// Returns whether the given tile index is solid for collision purposes. Out-of-range
    /// indices (including <see cref="Tilemap.EmptyTile"/>) are never solid.
    /// </summary>
    public bool IsSolid(int tileIndex) =>
        _solid is not null && tileIndex >= 0 && tileIndex < _solid.Length && _solid[tileIndex];

    /// <summary>
    /// Gets the zero-based indices of every tile marked solid via the constructor's
    /// <c>solidTileIndices</c> parameter, in ascending order.
    /// </summary>
    public IEnumerable<int> SolidTileIndices
    {
        get
        {
            if (_solid is null)
                yield break;

            for (var i = 0; i < _solid.Length; i++)
            {
                if (_solid[i])
                    yield return i;
            }
        }
    }
}

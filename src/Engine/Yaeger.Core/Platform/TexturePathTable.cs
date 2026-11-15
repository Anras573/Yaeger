namespace Yaeger.Platform;

/// <summary>
/// Interns texture paths to small integer ids so a render surface can detect batch boundaries
/// with an int compare instead of a string compare. Ids are stable for the table's lifetime
/// (a texture reloaded in place keeps its path, hence its id).
/// </summary>
/// <remarks>
/// Consecutive submissions overwhelmingly repeat the previous path, so the last path is checked
/// by reference first and the dictionary is only consulted on a change.
/// </remarks>
public sealed class TexturePathTable
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly List<string> _paths = [];
    private string? _lastPath;
    private int _lastId;

    /// <summary>Number of distinct paths interned so far.</summary>
    public int Count => _paths.Count;

    /// <summary>Returns the id for <paramref name="path"/>, assigning one on first use.</summary>
    public int GetId(string path)
    {
        if (ReferenceEquals(path, _lastPath))
            return _lastId;

        if (!_ids.TryGetValue(path, out var id))
        {
            id = _paths.Count;
            _paths.Add(path);
            _ids[path] = id;
        }

        _lastPath = path;
        _lastId = id;
        return id;
    }

    /// <summary>Returns the path an id was assigned to.</summary>
    public string GetPath(int id) => _paths[id];
}

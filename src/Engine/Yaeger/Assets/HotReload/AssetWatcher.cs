using Yaeger.Systems;

namespace Yaeger.Assets.HotReload;

/// <summary>
/// Opt-in, dev-time watcher that debounces raw file-change notifications from an
/// <see cref="IFileChangeSource"/> and dispatches them as logical asset paths — relative to
/// <c>root</c>, with forward slashes, matching the paths used by <c>Sprite.TexturePath</c>,
/// <c>Material3D</c> texture paths, and scene JSON — once a file has been quiet for
/// <c>debounceSeconds</c>. Editors commonly write a file more than once per save (temp file +
/// rename, or multiple flushes); debouncing coalesces that burst into a single dispatch.
/// </summary>
/// <remarks>
/// <see cref="Update"/> must be called from the main thread every frame (e.g. from
/// <c>window.OnUpdate</c>). A real <see cref="FileSystemWatcherSource"/> raises
/// <see cref="IFileChangeSource.Changed"/> on a thread-pool thread; <see cref="Update"/> is what
/// turns those into main-thread <see cref="AssetChanged"/> dispatches, which matters because
/// reload handlers (texture re-upload, etc.) make GL calls that must happen there.
/// </remarks>
public sealed class AssetWatcher : IUpdateSystem, IDisposable
{
    private readonly IFileChangeSource _source;
    private readonly string _root;
    private readonly float _debounceSeconds;
    private readonly object _lock = new();
    private readonly Dictionary<string, float> _pending = [];

    /// <summary>
    /// Raised on the thread that calls <see cref="Update"/> with each debounced asset's logical
    /// path (relative to the watched root, forward-slash separated).
    /// </summary>
    public event Action<string>? AssetChanged;

    /// <param name="source">Raw change notification source (mockable for tests).</param>
    /// <param name="root">
    /// Directory the raw paths from <paramref name="source"/> are made relative to when
    /// dispatched via <see cref="AssetChanged"/>.
    /// </param>
    /// <param name="debounceSeconds">
    /// How long a path must go without a new raw notification before it is dispatched.
    /// </param>
    public AssetWatcher(IFileChangeSource source, string root, float debounceSeconds = 0.25f)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _source = source;
        _root = root;
        _debounceSeconds = debounceSeconds;
        _source.Changed += OnRawChange;
    }

    /// <summary>
    /// Convenience factory that watches <paramref name="root"/> (default: the app's base
    /// directory, matching <c>AssetPath.Resolve</c>) via a real <see cref="FileSystemWatcherSource"/>.
    /// </summary>
    public static AssetWatcher ForDirectory(string? root = null, float debounceSeconds = 0.25f)
    {
        root ??= AppContext.BaseDirectory;
        return new AssetWatcher(new FileSystemWatcherSource(root), root, debounceSeconds);
    }

    private void OnRawChange(string absolutePath)
    {
        var logicalPath = Path.GetRelativePath(_root, absolutePath).Replace('\\', '/');

        lock (_lock)
        {
            _pending[logicalPath] = 0f;
        }
    }

    /// <summary>
    /// Advances debounce timers by <paramref name="deltaTime"/> and fires
    /// <see cref="AssetChanged"/> for every path that has gone quiet for at least the configured
    /// debounce window since its last raw notification.
    /// </summary>
    public void Update(float deltaTime)
    {
        List<string>? ready = null;

        lock (_lock)
        {
            foreach (var path in _pending.Keys.ToList())
            {
                var elapsed = _pending[path] + deltaTime;
                if (elapsed >= _debounceSeconds)
                {
                    _pending.Remove(path);
                    ready ??= [];
                    ready.Add(path);
                }
                else
                {
                    _pending[path] = elapsed;
                }
            }
        }

        if (ready is null)
            return;

        foreach (var path in ready)
            AssetChanged?.Invoke(path);
    }

    public void Dispose() => _source.Dispose();
}

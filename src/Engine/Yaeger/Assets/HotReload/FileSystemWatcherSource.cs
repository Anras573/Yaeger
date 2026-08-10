namespace Yaeger.Assets.HotReload;

/// <summary>
/// <see cref="IFileChangeSource"/> backed by a real <see cref="FileSystemWatcher"/>, watching a
/// directory tree recursively for file writes, creations, and renames.
/// </summary>
public sealed class FileSystemWatcherSource : IFileChangeSource
{
    private readonly FileSystemWatcher _watcher;

    public event Action<string>? Changed;

    /// <param name="root">Directory to watch, recursively.</param>
    public FileSystemWatcherSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter =
                NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        _watcher.Changed += (_, e) => Changed?.Invoke(e.FullPath);
        _watcher.Created += (_, e) => Changed?.Invoke(e.FullPath);
        _watcher.Renamed += (_, e) => Changed?.Invoke(e.FullPath);
        _watcher.Error += (_, e) =>
            Console.Error.WriteLine(
                $"[AssetWatcher] File system watcher error: {e.GetException().Message}"
            );

        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose() => _watcher.Dispose();
}

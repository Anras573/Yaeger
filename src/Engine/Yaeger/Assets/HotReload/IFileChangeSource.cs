namespace Yaeger.Assets.HotReload;

/// <summary>
/// Raw source of file-changed notifications. <see cref="Changed"/> may fire from any thread
/// (a real <see cref="System.IO.FileSystemWatcher"/> raises events on a thread-pool thread) and
/// may fire more than once for a single logical save. <see cref="AssetWatcher"/> is what debounces
/// and marshals these onto the caller's update thread.
/// </summary>
public interface IFileChangeSource : IDisposable
{
    /// <summary>Raised with the absolute path of the file that changed.</summary>
    event Action<string>? Changed;
}

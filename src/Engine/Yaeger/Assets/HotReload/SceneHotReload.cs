using Yaeger.ECS;

namespace Yaeger.Assets.HotReload;

/// <summary>
/// Opt-in helper that re-loads a scene file on request and hands the fresh <see cref="Scene"/> to
/// the caller via <see cref="Reloaded"/>, rather than mutating the world itself — deciding which
/// live entities to destroy or respawn is game-owned state this class has no business touching.
/// </summary>
/// <param name="loader">Loader used to re-parse the scene file.</param>
/// <param name="path">The scene path this instance reloads, as passed to <see cref="SceneLoader.Load"/>.</param>
public sealed class SceneHotReload(SceneLoader loader, string path)
{
    /// <summary>The scene path this instance reloads.</summary>
    public string Path { get; } = path;

    /// <summary>Raised with the freshly-loaded <see cref="Scene"/> after a successful <see cref="Reload"/>.</summary>
    public event Action<Scene>? Reloaded;

    /// <summary>
    /// Re-reads and re-parses the scene file. On success, raises <see cref="Reloaded"/> with the
    /// new <see cref="Scene"/> — apply it to the world however the game sees fit (e.g. destroy the
    /// entities created from the previous load, then <c>world.Instantiate(scene)</c>). A malformed
    /// or mid-write file is survived: the failure is logged and <see cref="Reloaded"/> does not
    /// fire, leaving whatever the world already has untouched.
    /// </summary>
    public void Reload()
    {
        Scene scene;
        try
        {
            scene = loader.Load(Path);
        }
        catch (Exception ex) when (ex is SceneLoadException or IOException)
        {
            Console.Error.WriteLine(
                $"[AssetWatcher] Failed to reload scene '{Path}': {ex.Message}"
            );
            return;
        }

        Reloaded?.Invoke(scene);
    }
}

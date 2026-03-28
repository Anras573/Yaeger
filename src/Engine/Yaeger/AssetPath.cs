namespace Yaeger;

/// <summary>
/// Resolves asset file paths relative to the application's base directory.
/// </summary>
/// <remarks>
/// When assets are configured with <c>CopyToOutputDirectory</c> in the project file,
/// they are placed alongside the application binary. This helper ensures that relative
/// paths like <c>"Assets/square.png"</c> are resolved against that output directory
/// rather than the current working directory, which may differ when using
/// <c>dotnet run</c> or launching from an IDE.
/// </remarks>
public static class AssetPath
{
    /// <summary>
    /// Resolves an asset path against the application's base directory.
    /// Absolute paths are returned unchanged.
    /// </summary>
    /// <param name="path">
    /// The asset path to resolve. May be relative (e.g. <c>"Assets/square.png"</c>) or an absolute path.
    /// </param>
    /// <returns>The fully resolved absolute path.</returns>
    public static string Resolve(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}

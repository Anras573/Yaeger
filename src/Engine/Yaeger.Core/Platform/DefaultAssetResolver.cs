namespace Yaeger.Platform;

/// <summary>
/// Default path resolver that uses <see cref="AssetPath"/>.
/// </summary>
public sealed class DefaultAssetResolver : IAssetResolver
{
    public string Resolve(string path) => AssetPath.Resolve(path);
}

namespace Yaeger.Platform;

/// <summary>
/// Native adapter that resolves assets against the app output directory.
/// </summary>
public sealed class NativeAssetResolver : IAssetResolver
{
    public string Resolve(string path) => AssetPath.Resolve(path);
}

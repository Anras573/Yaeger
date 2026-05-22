namespace Yaeger.Platform;

/// <summary>
/// Resolves logical asset paths to concrete locations.
/// </summary>
public interface IAssetResolver
{
    string Resolve(string path);
}

namespace Yaeger.Font;

public class FontManager : IDisposable
{
    private readonly Dictionary<string, Font> _fonts = new();
    private bool _disposed;

    /// <summary>
    /// Loads a font from a URL over HTTP, caching the result by URL.
    /// </summary>
    /// <param name="url">Absolute HTTP/HTTPS URL of the font file.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="FontLoadException">
    /// When the request fails or returns a non-success HTTP status.
    /// </exception>
    public async Task<Font> LoadAsync(
        string url,
        HttpClient httpClient,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!IsAbsoluteUrl(url))
            throw new ArgumentException("Must be an absolute http or https URL.", nameof(url));

        if (_fonts.TryGetValue(url, out var cached))
            return cached;

        try
        {
            using var response = await httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new FontLoadException(
                    $"HTTP {(int)response.StatusCode} fetching font from '{url}'."
                );

            var bytes = await response
                .Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            var font = new Font(url, bytes);
            _fonts[url] = font;
            return font;
        }
        catch (HttpRequestException ex)
        {
            throw new FontLoadException($"Failed to fetch font from '{url}': {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FontLoadException($"Request timed out fetching font from '{url}'.", ex);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or FontLoadException))
        {
            throw new FontLoadException($"Failed to load font from '{url}': {ex.Message}", ex);
        }
    }

    public Font Load(string fontPath)
    {
        var key = NormalizeKey(fontPath);
        if (_fonts.TryGetValue(key, out var existingFont))
            return existingFont;

        if (IsAbsoluteUrl(fontPath))
            throw new ArgumentException(
                "URL fonts must be fetched via LoadAsync.",
                nameof(fontPath)
            );

        var font = new Font(key);
        _fonts[key] = font;
        return font;
    }

    public Font? Get(string fontPath)
    {
        var key = NormalizeKey(fontPath);
        return _fonts.TryGetValue(key, out var font) ? font : null;
    }

    public void Unload(string fontPath)
    {
        var key = NormalizeKey(fontPath);
        if (_fonts.TryGetValue(key, out var font))
        {
            font.Dispose();
            _fonts.Remove(key);
        }
    }

    private static bool IsAbsoluteUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKey(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IsAbsoluteUrl(path) ? path : AssetPath.Resolve(path);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _fonts.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

namespace Yaeger.Font;

public class FontManager : IDisposable
{
    private readonly Dictionary<string, Font> _fonts = new();
    private readonly object _lock = new();
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

        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
        )
            throw new ArgumentException("Must be an absolute http or https URL.", nameof(url));

        var cacheKey = uri.AbsoluteUri;
        lock (_lock)
        {
            if (_fonts.TryGetValue(cacheKey, out var cached))
                return cached;
        }

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
            var font = new Font(cacheKey, bytes);
            lock (_lock)
            {
                if (_fonts.TryGetValue(cacheKey, out var existing))
                {
                    font.Dispose();
                    return existing;
                }
                _fonts[cacheKey] = font;
                return font;
            }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath, nameof(fontPath));

        if (IsAbsoluteUrl(fontPath))
            throw new ArgumentException(
                "URL fonts must be fetched via LoadAsync.",
                nameof(fontPath)
            );

        var key = AssetPath.Resolve(fontPath);
        lock (_lock)
        {
            if (_fonts.TryGetValue(key, out var existingFont))
                return existingFont;

            var font = new Font(key);
            _fonts[key] = font;
            return font;
        }
    }

    public Font? Get(string fontPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath, nameof(fontPath));
        var key = NormalizeKey(fontPath);
        lock (_lock)
        {
            return _fonts.TryGetValue(key, out var font) ? font : null;
        }
    }

    public void Unload(string fontPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath, nameof(fontPath));
        var key = NormalizeKey(fontPath);
        lock (_lock)
        {
            if (_fonts.TryGetValue(key, out var font))
            {
                font.Dispose();
                _fonts.Remove(key);
            }
        }
    }

    private static bool IsAbsoluteUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKey(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is ("http" or "https"))
            return uri.AbsoluteUri;
        return AssetPath.Resolve(path);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            foreach (var font in _fonts.Values)
                font.Dispose();

            _fonts.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

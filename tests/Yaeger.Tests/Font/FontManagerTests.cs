using System.Net;
using Yaeger.Font;

namespace Yaeger.Tests.Font;

public class FontManagerTests : IDisposable
{
    private readonly FontManager _manager = new();

    public void Dispose() => _manager.Dispose();

    // ── Argument validation ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NullHttpClient_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.LoadAsync("http://example.com/font.ttf", null!)
        );
    }

    [Fact]
    public async Task LoadAsync_EmptyUrl_ThrowsArgumentException()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, []);

        await Assert.ThrowsAsync<ArgumentException>(() => _manager.LoadAsync("", httpClient));
    }

    [Fact]
    public async Task LoadAsync_RelativePath_ThrowsArgumentException()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, []);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.LoadAsync("relative/font.ttf", httpClient)
        );
    }

    [Fact]
    public async Task LoadAsync_NonHttpScheme_ThrowsArgumentException()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, []);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.LoadAsync("ftp://example.com/font.ttf", httpClient)
        );
    }

    // ── HTTP error responses ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NotFoundResponse_ThrowsFontLoadExceptionWithStatusCode()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.NotFound, []);

        var ex = await Assert.ThrowsAsync<FontLoadException>(() =>
            _manager.LoadAsync("http://example.com/missing.ttf", httpClient)
        );
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_ServerError_ThrowsFontLoadExceptionWithStatusCode()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.InternalServerError, []);

        var ex = await Assert.ThrowsAsync<FontLoadException>(() =>
            _manager.LoadAsync("http://example.com/error.ttf", httpClient)
        );
        Assert.Contains("500", ex.Message);
    }

    // ── Network failure ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NetworkFailure_ThrowsFontLoadException()
    {
        using var httpClient = MakeThrowingClient();

        await Assert.ThrowsAsync<FontLoadException>(() =>
            _manager.LoadAsync("http://example.com/font.ttf", httpClient)
        );
    }

    // ── Success and caching ───────────────────────────────────────────────────

    [SkippableFact]
    public async Task LoadAsync_ValidFontBytes_ReturnsFontAndCaches()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Roboto-Regular.ttf");
        Skip.IfNot(File.Exists(fontPath), "Roboto-Regular.ttf test asset is missing.");
        Skip.IfNot(IsHarfBuzzAvailable(), "HarfBuzz native library not available.");

        var fontBytes = File.ReadAllBytes(fontPath);
        using var manager = new FontManager();
        using var firstClient = MakeFakeClient(HttpStatusCode.OK, fontBytes);
        using var secondClient = MakeThrowingClient();

        var font1 = await manager.LoadAsync("http://example.com/font.ttf", firstClient);
        var font2 = await manager.LoadAsync("http://example.com/font.ttf", secondClient);

        Assert.NotNull(font1);
        Assert.Same(font1, font2);
    }

    private static bool IsHarfBuzzAvailable()
    {
        var dir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(dir, "libHarfBuzzSharp.so"))
            || File.Exists(
                Path.Combine(dir, "runtimes", "linux-x64", "native", "libHarfBuzzSharp.so")
            );
    }

    // ── Load argument validation ──────────────────────────────────────────────

    [Fact]
    public void Load_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.Load(null!));
    }

    [Fact]
    public void Load_WhitespacePath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _manager.Load("   "));
    }

    [Fact]
    public void Load_UrlPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _manager.Load("http://example.com/font.ttf"));
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.Get(null!));
    }

    [Fact]
    public void Get_UnknownPath_ReturnsNull()
    {
        Assert.Null(_manager.Get("nonexistent.ttf"));
    }

    // ── Invalid/empty payload ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_EmptyBytesResponse_ThrowsFontLoadException()
    {
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, []);

        await Assert.ThrowsAsync<FontLoadException>(() =>
            _manager.LoadAsync("http://example.com/empty.ttf", httpClient)
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpClient MakeFakeClient(HttpStatusCode statusCode, byte[] content) =>
        new(new FakeHttpMessageHandler(statusCode, content));

    private static HttpClient MakeThrowingClient() => new(new ThrowingHttpMessageHandler());

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, byte[] content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(content) }
            );
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("Simulated network failure.");
    }
}

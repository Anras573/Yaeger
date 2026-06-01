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

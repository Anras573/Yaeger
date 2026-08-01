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

        // On Linux the natives are guaranteed: HarfBuzzSharp.NativeAssets.Linux is referenced and
        // covers every mainstream Linux RID (glibc and musl, x86 through riscv64). So a missing
        // library there is a broken package reference, not an unsupported platform — assert rather
        // than skip, since a skip reads as a green CI run and is exactly how this test sat dormant
        // before. Other platforms still skip: their natives come from packages this repo doesn't
        // control the RID coverage of.
        if (OperatingSystem.IsLinux())
        {
            Assert.True(
                IsHarfBuzzAvailable(),
                "HarfBuzz native library failed to load on Linux, where "
                    + "HarfBuzzSharp.NativeAssets.Linux should have supplied it."
            );
        }
        else
        {
            Skip.IfNot(IsHarfBuzzAvailable(), "HarfBuzz native library not available.");
        }

        var fontBytes = File.ReadAllBytes(fontPath);
        using var manager = new FontManager();
        using var firstClient = MakeFakeClient(HttpStatusCode.OK, fontBytes);
        using var secondClient = MakeThrowingClient();

        var font1 = await manager.LoadAsync("http://example.com/font.ttf", firstClient);
        var font2 = await manager.LoadAsync("http://example.com/font.ttf", secondClient);

        Assert.NotNull(font1);
        Assert.Same(font1, font2);
    }

    // Mirrors AssimpLoaderTests.IsAssimpAvailable: force the native library to load and report
    // whether it did. Probing for a file path instead would have to hardcode both the platform's
    // library naming (.so/.dylib/.dll) and every RID subfolder it might sit in — the previous
    // version checked only linux-x64's .so, so this test skipped unconditionally on Windows and
    // macOS even when HarfBuzz was perfectly usable there.
    private static bool IsHarfBuzzAvailable()
    {
        try
        {
            // Allocating a buffer makes a native call, forcing library load.
            using var buffer = new HarfBuzzSharp.Buffer();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (TypeInitializationException)
        {
            // The native load failure can surface wrapped in a static-ctor failure.
            return false;
        }
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

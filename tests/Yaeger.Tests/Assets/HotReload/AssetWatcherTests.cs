using Yaeger.Assets.HotReload;

namespace Yaeger.Tests.Assets.HotReload;

// Pure debounce/dispatch logic — the FileSystemWatcher-backed IFileChangeSource is faked so this
// runs deterministically without touching the real filesystem or a wall clock.
public class AssetWatcherTests
{
    private sealed class FakeFileChangeSource : IFileChangeSource
    {
        public bool Disposed { get; private set; }

        public event Action<string>? Changed;

        public void Raise(string absolutePath) => Changed?.Invoke(absolutePath);

        public void Dispose() => Disposed = true;
    }

    private static readonly string Root = Path.Combine(
        Path.DirectorySeparatorChar.ToString(),
        "assets"
    );

    private static string AbsolutePath(string relative) => Path.Combine(Root, relative);

    [Fact]
    public void Update_BeforeDebounceWindowElapses_DoesNotDispatch()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root, debounceSeconds: 0.3f);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        source.Raise(AbsolutePath("player.png"));
        watcher.Update(0.2f);

        Assert.Empty(changed);
    }

    [Fact]
    public void Update_AfterDebounceWindowElapses_DispatchesLogicalPath()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root, debounceSeconds: 0.3f);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        source.Raise(AbsolutePath("player.png"));
        watcher.Update(0.2f);
        watcher.Update(0.2f);

        Assert.Equal(["player.png"], changed);
    }

    [Fact]
    public void Update_RepeatedRawChangesWithinWindow_ResetsTimerAndDispatchesOnce()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root, debounceSeconds: 0.3f);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        // Simulates an editor writing the same file twice in quick succession.
        source.Raise(AbsolutePath("player.png"));
        watcher.Update(0.2f);
        source.Raise(AbsolutePath("player.png"));
        watcher.Update(0.2f);

        Assert.Empty(changed); // only 0.2s quiet since the second write — still inside the window

        watcher.Update(0.2f);

        Assert.Equal(["player.png"], changed);
    }

    [Fact]
    public void Update_MultipleDistinctPaths_DispatchesEachIndependently()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root, debounceSeconds: 0.3f);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        source.Raise(AbsolutePath("player.png"));
        watcher.Update(0.1f);
        source.Raise(AbsolutePath("level1.json"));
        watcher.Update(0.3f);

        // player.png has been quiet 0.4s (dispatched); level1.json only 0.3s (also dispatched,
        // since it just reached the threshold) — both fire once each, no duplicates.
        Assert.Equal(2, changed.Count);
        Assert.Contains("player.png", changed);
        Assert.Contains("level1.json", changed);
    }

    [Fact]
    public void Update_NestedPath_NormalizesToForwardSlashes()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root, debounceSeconds: 0.1f);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        source.Raise(AbsolutePath(Path.Combine("Textures", "player.png")));
        watcher.Update(0.2f);

        Assert.Equal(["Textures/player.png"], changed);
    }

    [Fact]
    public void Update_NoPendingChanges_DoesNotDispatch()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root);
        var changed = new List<string>();
        watcher.AssetChanged += changed.Add;

        watcher.Update(10f);

        Assert.Empty(changed);
    }

    [Fact]
    public void Dispose_DisposesUnderlyingSource()
    {
        var source = new FakeFileChangeSource();
        var watcher = new AssetWatcher(source, Root);

        watcher.Dispose();

        Assert.True(source.Disposed);
    }
}

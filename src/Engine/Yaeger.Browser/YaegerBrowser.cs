using System.Runtime.InteropServices.JavaScript;

namespace Yaeger.Browser;

/// <summary>
/// Entry point for hosting Yaeger in the browser: loads the packaged <c>yaeger-browser.js</c>
/// module and runs the <c>requestAnimationFrame</c> pump.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
public static partial class YaegerBrowser
{
    /// <summary>Module name the <c>[JSImport]</c> declarations bind to.</summary>
    internal const string ModuleName = "yaeger-browser";

    /// <summary>Path of the packaged module, relative to the app base URI.</summary>
    public const string ModulePath = "_content/Yaeger.Browser/yaeger-browser.js";

    /// <summary>
    /// Imports the packaged JS module so every <c>[JSImport]</c> function becomes available.
    /// </summary>
    /// <param name="baseUri">
    /// The app base URI (e.g. <c>NavigationManager.BaseUri</c>). The module URL is resolved
    /// against it, so apps hosted under a sub-path work.
    /// </param>
    public static Task InitializeAsync(string baseUri) =>
        JSHost.ImportAsync(ModuleName, BuildModuleUrl(baseUri));

    internal static string BuildModuleUrl(string baseUri)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseUri);
        return (baseUri.EndsWith('/') ? baseUri : baseUri + "/") + ModulePath;
    }

    /// <summary>
    /// Starts the <c>requestAnimationFrame</c> loop, invoking <paramref name="tick"/> once per
    /// frame with the <c>DOMHighResTimeStamp</c> in milliseconds. No-op if already running.
    /// Requires <see cref="InitializeAsync"/> to have completed.
    /// </summary>
    public static void StartGameLoop(Action<double> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        Interop.JsInterop.StartGameLoop(tick);
    }

    /// <summary>Stops the loop started by <see cref="StartGameLoop"/>.</summary>
    public static void StopGameLoop() => Interop.JsInterop.StopGameLoop();
}

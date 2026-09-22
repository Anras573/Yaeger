using FeatureGallery;
using FeatureGallery.Scenes.BouncingBalls;
using FeatureGallery.Scenes.Mouse;
using FeatureGallery.Scenes.TextRendering;
using Yaeger.Windowing;

// FeatureGallery: one window, a menu listing small single-feature demo scenes, switch between
// them at runtime. Pass --scene <Name> to launch straight into a scene, e.g.:
//   dotnet run --project Samples/FeatureGallery/FeatureGallery.csproj -- --scene Mouse

using var window = Window.Create();

Func<IDemoScene>[] scenes =
[
    () => new MouseScene(),
    () => new TextRenderingScene(),
    () => new BouncingBallsScene(),
];

_ = new SceneHost(window, scenes, ParseSceneArg(args));

window.Run();

return;

static string? ParseSceneArg(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--scene", StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

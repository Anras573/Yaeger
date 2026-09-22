using System.Numerics;
using Yaeger.Windowing;

namespace FeatureGallery;

/// <summary>
/// A single small, single-feature demo scene hosted by <see cref="SceneHost"/>. Each scene owns
/// its own <c>World</c> and rendering resources — nothing is shared across scenes. A scene must
/// not bind <c>Keys.Escape</c>; the host owns it (returns to the menu / exits from the menu).
/// </summary>
public interface IDemoScene : IDisposable
{
    /// <summary>Shown as the menu button label.</summary>
    string Name { get; }

    /// <summary>One line, shown under the name in the menu.</summary>
    string Description { get; }

    /// <summary>Shown as a HUD overlay while the scene runs.</summary>
    string Controls { get; }

    /// <summary>
    /// Creates the scene's <c>World</c>, systems, and entities, and binds any input this scene
    /// needs. Called once per scene entry; a fresh <see cref="IDemoScene"/> instance is used
    /// each time so re-entering a scene starts from a clean slate.
    /// </summary>
    void Load(Window window);

    void Update(float deltaTime);
    void Render(float deltaTime);

    /// <summary>Called on window resize while this scene is active.</summary>
    void Resize(Vector2 size) { }
}

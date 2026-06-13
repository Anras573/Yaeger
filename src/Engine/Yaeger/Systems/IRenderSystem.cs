namespace Yaeger.Systems;

/// <summary>
/// Interface for systems that issue draw calls each frame.
/// </summary>
public interface IRenderSystem
{
    /// <summary>
    /// Submits all draw calls for this system's entities.
    /// Call from your window's render callback after update systems have run.
    /// </summary>
    void Render();
}

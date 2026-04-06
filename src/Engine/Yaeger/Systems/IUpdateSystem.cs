namespace Yaeger.Systems;

/// <summary>
/// Interface for systems that update game state each frame.
/// </summary>
public interface IUpdateSystem
{
    /// <summary>
    /// Updates the system state.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last update, in seconds.</param>
    void Update(float deltaTime);
}

namespace Yaeger.Graphics;

/// <summary>
/// Component that controls deterministic draw order for renderable entities.
/// Lower values render first; higher values render on top.
/// </summary>
public readonly record struct RenderLayer(int Value = 0);

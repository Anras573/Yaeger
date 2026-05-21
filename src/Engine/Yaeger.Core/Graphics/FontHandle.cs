namespace Yaeger.Graphics;

/// <summary>
/// Simple immutable font handle that can be used in core-only contexts.
/// </summary>
public readonly record struct FontHandle(string Id) : IFontHandle;

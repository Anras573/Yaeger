namespace Yaeger.Graphics;

/// <summary>
/// Simple immutable font handle that can be used in core-only contexts.
/// </summary>
public readonly record struct FontHandle : IFontHandle
{
    private readonly string? _id;

    public FontHandle(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Font handle id cannot be null, empty, or whitespace.",
                nameof(id)
            );
        }

        _id = id;
    }

    public string Id =>
        _id
        ?? throw new InvalidOperationException(
            "Font handle is uninitialized. Construct FontHandle with a non-empty id."
        );
}

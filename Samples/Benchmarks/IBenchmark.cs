namespace Benchmarks;

/// <summary>
/// A single perf smoke-test, selected by command-line arg and run to completion in its own
/// process — see <c>Program.cs</c>. Copied from FeatureGallery's <c>IDemoScene</c> shape rather
/// than referenced from it: benchmarks deliberately live in a separate app with no shared menu
/// state that could skew the numbers.
/// </summary>
public interface IBenchmark
{
    /// <summary>The command-line arg that selects this benchmark.</summary>
    string Arg { get; }

    /// <summary>One line, shown in the usage listing.</summary>
    string Description { get; }

    /// <summary>The main entity count used when no count arg is given.</summary>
    int DefaultCount { get; }

    /// <summary>
    /// Creates its own <c>Window</c>, builds the scene sized by <paramref name="count"/>, and
    /// runs the window's loop to completion (blocks until the window closes).
    /// </summary>
    void Run(int count);
}

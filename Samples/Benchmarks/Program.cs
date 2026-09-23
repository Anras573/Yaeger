using Benchmarks;
using Benchmarks.Crowd;
using Benchmarks.Instancing;
using Benchmarks.Sprites;

// Benchmarks: perf smoke tests, one clean process per benchmark — no menu UI or leftover state
// from other scenes that could skew the numbers. Select one via a command-line arg:
//
//   dotnet run --project Samples/Benchmarks -- sprites|instancing|crowd [count]
//
// `count` is optional and overrides the benchmark's default entity count (e.g. `-- crowd 400`).

IBenchmark[] benchmarks = [new SpritesBenchmark(), new InstancingBenchmark(), new CrowdBenchmark()];

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var benchmark = benchmarks.FirstOrDefault(b =>
    string.Equals(b.Arg, args[0], StringComparison.OrdinalIgnoreCase)
);

if (benchmark is null)
{
    Console.Error.WriteLine($"Unknown benchmark: '{args[0]}'");
    PrintUsage();
    return;
}

var count = benchmark.DefaultCount;
if (args.Length > 1)
{
    if (!int.TryParse(args[1], out count) || count <= 0)
    {
        Console.Error.WriteLine($"Invalid count: '{args[1]}' (expected a positive integer)");
        return;
    }
}

benchmark.Run(count);

void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run --project Samples/Benchmarks -- <benchmark> [count]");
    Console.WriteLine();
    Console.WriteLine("Benchmarks:");
    foreach (var b in benchmarks)
    {
        Console.WriteLine($"  {b.Arg, -12} {b.Description} (default count: {b.DefaultCount})");
    }
}

using System.Diagnostics;
using System.Numerics;
using Yaeger.ECS;

namespace Benchmarks.Ecs;

/// <summary>
/// Headless micro-benchmark for ECS iteration (issue #337): times 2- and 3-component
/// <c>world.Query</c> passes over N entities against a baseline that reproduces the previous
/// design (<c>Dictionary&lt;Entity, T&gt;</c> stores walked through <c>IEnumerable</c> iterator
/// methods), and reports allocated bytes per pass. Needs no window or display.
/// </summary>
public sealed class EcsBenchmark : IBenchmark
{
    private struct A
    {
        public Vector2 Value;
    }

    private struct B
    {
        public Vector2 Value;
    }

    private struct C
    {
        public float Value;
    }

    // A transform-sized component, so the cost of copying components out of the stores shows up
    // (A/B/C are small enough that it mostly doesn't).
    private struct Wide
    {
        public Matrix4x4 Value;
    }

    public string Arg => "ecs";

    public string Description =>
        "Headless ECS query iteration: sparse-set struct enumerators vs the old dictionary + iterator design (issue #337).";

    public int DefaultCount => 100_000;

    public void Run(int count)
    {
        var world = new World();
        var dictA = new Dictionary<Entity, A>();
        var dictB = new Dictionary<Entity, B>();
        var dictC = new Dictionary<Entity, C>();
        var dictWide = new Dictionary<Entity, Wide>();
        for (var i = 0; i < count; i++)
        {
            var e = world.CreateEntity();
            var a = new A { Value = new Vector2(i, 1) };
            var b = new B { Value = new Vector2(1, i) };
            var c = new C { Value = i };
            world.AddComponent(e, a);
            world.AddComponent(e, b);
            world.AddComponent(e, c);
            dictA[e] = a;
            dictB[e] = b;
            dictC[e] = c;
            var wide = new Wide { Value = Matrix4x4.CreateTranslation(i, 1, 0) };
            world.AddComponent(e, wide);
            dictWide[e] = wide;
        }

        Console.WriteLine($"ECS iteration, {count:N0} entities (ms/pass, bytes allocated/pass)");
        Console.WriteLine();

        Measure(
            "Query<A,B>      sparse-set",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b) in world.Query<A, B>())
                    sum += a.Value.X + b.Value.Y;
                return sum;
            }
        );
        Measure(
            "Query<A,B>      baseline  ",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b) in BaselineQuery2(dictA, dictB))
                    sum += a.Value.X + b.Value.Y;
                return sum;
            }
        );
        Measure(
            "Query<A,B,C>    sparse-set",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b, c) in world.Query<A, B, C>())
                    sum += a.Value.X + b.Value.Y + c.Value;
                return sum;
            }
        );
        Measure(
            "Query<A,B,C>    baseline  ",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b, c) in BaselineQuery3(dictA, dictB, dictC))
                    sum += a.Value.X + b.Value.Y + c.Value;
                return sum;
            }
        );
        Measure(
            "Query<A,B,Wide> sparse-set",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b, w) in world.Query<A, B, Wide>())
                    sum += a.Value.X + b.Value.Y + w.Value.M41;
                return sum;
            }
        );
        Measure(
            "Query<A,B,Wide> baseline  ",
            () =>
            {
                var sum = 0f;
                foreach (var (_, a, b, w) in BaselineQuery3(dictA, dictB, dictWide))
                    sum += a.Value.X + b.Value.Y + w.Value.M41;
                return sum;
            }
        );
    }

    private static void Measure(string name, Func<float> pass)
    {
        const int warmup = 3;
        const int passes = 20;
        var sink = 0f;
        for (var i = 0; i < warmup; i++)
            sink += pass();

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < passes; i++)
            sink += pass();
        sw.Stop();
        var allocated = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / passes;

        Console.WriteLine(
            $"{name}  {sw.Elapsed.TotalMilliseconds / passes, 8:F3} ms  {allocated, 10:N0} B  (checksum {sink:G4})"
        );
    }

    // The pre-#337 shape: iterator methods over dictionary stores, yielding value tuples.
    private static IEnumerable<(Entity, A, B)> BaselineQuery2(
        Dictionary<Entity, A> a,
        Dictionary<Entity, B> b
    )
    {
        foreach (var (entity, ca) in a)
            if (b.TryGetValue(entity, out var cb))
                yield return (entity, ca, cb);
    }

    private static IEnumerable<(Entity, A, B, T)> BaselineQuery3<T>(
        Dictionary<Entity, A> a,
        Dictionary<Entity, B> b,
        Dictionary<Entity, T> c
    )
    {
        foreach (var (entity, ca) in a)
            if (b.TryGetValue(entity, out var cb) && c.TryGetValue(entity, out var cc))
                yield return (entity, ca, cb, cc);
    }
}

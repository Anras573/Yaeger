using System.Diagnostics.CodeAnalysis;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Stores prefiltered <see cref="EnvironmentMap"/> image-based-lighting resources, keyed by the
/// same <see cref="Skybox"/> handle used with <see cref="CubemapRegistry"/>. Mirrors
/// <see cref="CubemapRegistry"/>'s role, but for the derived IBL textures rather than the raw
/// skybox cubemap.
/// </summary>
public sealed class EnvironmentMapRegistry(CubemapRegistry cubemaps, IblPrefilter prefilter)
    : IDisposable
{
    private readonly Dictionary<int, EnvironmentMap> _environmentMaps = new();

    /// <summary>
    /// Prefilters the skybox cubemap already registered under <paramref name="handle"/> in the
    /// <see cref="CubemapRegistry"/> passed at construction, and stores the result for
    /// <see cref="TryGet"/>. Prefiltering runs once, synchronously, via offscreen GPU passes — it
    /// is not repeated automatically if the underlying cubemap changes later. Re-registering the
    /// same handle disposes the previous <see cref="EnvironmentMap"/> and replaces it.
    /// </summary>
    /// <param name="handle">The skybox handle to prefilter. Must already be registered with the
    /// backing <see cref="CubemapRegistry"/>.</param>
    /// <param name="viewportWidth">Current window viewport width, restored after the offscreen
    /// prefiltering passes (which resize the viewport to each capture resolution).</param>
    /// <param name="viewportHeight">Current window viewport height, restored the same way.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="handle"/> is not registered with the backing
    /// <see cref="CubemapRegistry"/>.
    /// </exception>
    public void Register(Skybox handle, int viewportWidth, int viewportHeight)
    {
        if (!cubemaps.TryGet(handle, out var cubemap))
            throw new ArgumentException(
                $"Skybox handle {handle.Id} is not registered with the backing CubemapRegistry.",
                nameof(handle)
            );

        if (_environmentMaps.Remove(handle.Id, out var existing))
            existing.Dispose();

        _environmentMaps[handle.Id] = prefilter.Prefilter(cubemap, viewportWidth, viewportHeight);
    }

    /// <summary>Looks up the prefiltered environment map for the given skybox handle.</summary>
    public bool TryGet(Skybox handle, [NotNullWhen(true)] out EnvironmentMap? environmentMap) =>
        _environmentMaps.TryGetValue(handle.Id, out environmentMap);

    public void Dispose()
    {
        foreach (var environmentMap in _environmentMaps.Values)
            environmentMap.Dispose();
        _environmentMaps.Clear();
    }
}

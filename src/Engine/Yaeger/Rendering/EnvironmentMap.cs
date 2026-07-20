using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// GPU resources for image-based lighting derived from a single skybox cubemap: a diffuse
/// irradiance cubemap, a roughness-to-mip prefiltered specular cubemap, and a reference to the
/// shared split-sum BRDF LUT. Produced by <see cref="IblPrefilter.Prefilter"/> and owned by
/// <see cref="EnvironmentMapRegistry"/>.
/// </summary>
public sealed class EnvironmentMap : IDisposable
{
    private readonly GL _gl;

    /// <summary>Diffuse irradiance cubemap handle.</summary>
    public uint IrradianceMap { get; }

    /// <summary>Roughness-to-mip prefiltered specular cubemap handle.</summary>
    public uint PrefilteredMap { get; }

    /// <summary>
    /// Number of mip levels in <see cref="PrefilteredMap"/>. Roughness 0 samples mip 0 (sharp);
    /// roughness 1 samples mip <c>PrefilteredMipCount - 1</c> (fully blurred).
    /// </summary>
    public int PrefilteredMipCount { get; }

    /// <summary>
    /// The split-sum BRDF LUT handle. Depends only on the BRDF (not the environment), so it is
    /// computed once by <see cref="IblPrefilter"/> and shared across every
    /// <see cref="EnvironmentMap"/> — this instance does not own or dispose it.
    /// </summary>
    public uint BrdfLut { get; }

    internal EnvironmentMap(
        GL gl,
        uint irradianceMap,
        uint prefilteredMap,
        int prefilteredMipCount,
        uint brdfLut
    )
    {
        _gl = gl;
        IrradianceMap = irradianceMap;
        PrefilteredMap = prefilteredMap;
        PrefilteredMipCount = prefilteredMipCount;
        BrdfLut = brdfLut;
    }

    /// <summary>Deletes the irradiance and prefiltered cubemaps. Does not delete the shared BRDF LUT.</summary>
    public void Dispose()
    {
        _gl.DeleteTexture(IrradianceMap);
        _gl.DeleteTexture(PrefilteredMap);
    }
}

using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Single-pass <see cref="IPostProcessEffect"/>: compresses HDR scene colour (values that may
/// exceed 1.0 — see <see cref="PostProcessStack"/>'s <c>hdr</c> constructor parameter and
/// <see cref="Graphics.Material3D.EmissiveIntensity"/>) down to the backbuffer's displayable
/// [0, 1] range via <see cref="Operator"/>, then gamma-encodes the result back to sRGB. Must be
/// the last enabled effect in the chain (<see cref="RequiresLastPass"/>, enforced by
/// <see cref="PostProcessPlanner.ValidateOrdering"/>) — every earlier effect operates on linear
/// HDR colour, and an effect placed after this one would instead see already-compressed,
/// gamma-encoded values. See docs/post-processing.md.
/// </summary>
public sealed class ToneMapEffect : IPostProcessEffect
{
    private static readonly string VertexSource = EmbeddedShaderSource.Load("PostProcessQuad.vert");
    private static readonly string FragmentSource = EmbeddedShaderSource.Load("ToneMap.frag");

    private readonly GL _gl;
    private readonly Shader _shader;

    public bool Enabled { get; set; } = true;

    public bool RequiresLastPass => true;

    /// <summary>Which tone-mapping curve compresses HDR colour into [0, 1]. Defaults to ACES filmic.</summary>
    public ToneMapOperator Operator { get; set; } = ToneMapOperator.AcesFilmic;

    /// <summary>Multiplier applied to the HDR colour before tone mapping. 1 = no exposure adjustment.</summary>
    public float Exposure { get; set; } = 1f;

    public ToneMapEffect(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexSource, FragmentSource);
    }

    public void Apply(
        uint sourceColorTexture,
        uint destinationFramebuffer,
        int width,
        int height,
        FullscreenQuad quad
    )
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, destinationFramebuffer);
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);

        _shader.Bind();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sourceColorTexture);
        _shader.SetUniformInt("uSource", 0);
        _shader.SetUniformFloat("uExposure", Exposure);
        _shader.SetUniformInt("uOperator", Operator == ToneMapOperator.AcesFilmic ? 1 : 0);
        quad.Draw();

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _shader.Unbind();
    }

    public void Dispose() => _shader.Dispose();
}

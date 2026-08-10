using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Single-pass <see cref="IPostProcessEffect"/>: darkens the frame towards the edges (vignette)
/// and applies a simple saturation + colour-tint grade. Demonstrates the simplest possible chain
/// entry — one shader, one draw, straight into whatever destination <see cref="PostProcessStack"/>
/// hands it.
/// </summary>
public sealed class VignetteEffect : IPostProcessEffect
{
    private static readonly string VertexSource = EmbeddedShaderSource.Load("PostProcessQuad.vert");
    private static readonly string FragmentSource = EmbeddedShaderSource.Load("Vignette.frag");

    private readonly GL _gl;
    private readonly Shader _shader;

    public bool Enabled { get; set; } = true;

    /// <summary>Strength of the edge darkening: 0 = none, 1 = fully black at the edges.</summary>
    public float Intensity { get; set; } = 0.4f;

    /// <summary>Distance from the frame's centre (in UV units, 0.5 = edge) where darkening starts.</summary>
    public float Radius { get; set; } = 0.4f;

    /// <summary>Width of the fade from full brightness to fully vignetted. Must stay positive.</summary>
    public float Softness { get; set; } = 0.5f;

    /// <summary>1 = unchanged colour, 0 = grayscale, greater than 1 = boosted saturation.</summary>
    public float Saturation { get; set; } = 1f;

    /// <summary>Multiplicative colour tint applied after the saturation grade. (1,1,1) = neutral.</summary>
    public Vector3 Tint { get; set; } = Vector3.One;

    public VignetteEffect(GL gl)
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
        _shader.SetUniformFloat("uIntensity", Intensity);
        _shader.SetUniformFloat("uRadius", Radius);
        _shader.SetUniformFloat("uSoftness", MathF.Max(Softness, 0.0001f));
        _shader.SetUniformFloat("uSaturation", Saturation);
        _shader.SetUniformVec3("uTint", Tint);
        quad.Draw();

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _shader.Unbind();
    }

    public void Dispose() => _shader.Dispose();
}

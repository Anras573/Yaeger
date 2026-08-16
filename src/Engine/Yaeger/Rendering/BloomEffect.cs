using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Multi-pass <see cref="IPostProcessEffect"/>: extracts pixels brighter than <see cref="Threshold"/>,
/// blurs them with a separable Gaussian kernel over <see cref="BlurIterations"/> horizontal/vertical
/// pairs (ping-ponging between two half-resolution offscreen targets it owns internally), then
/// composites the blurred result additively back onto the original source into whatever destination
/// <see cref="PostProcessStack"/> hands it. Demonstrates the multi-pass shape the outer stack's
/// single scene → effect → effect → backbuffer chain doesn't otherwise exercise.
/// </summary>
/// <remarks>
/// Never tone-maps or gamma-encodes — it only extracts, blurs, and adds back colour, so it works
/// unchanged whether it sits in an LDR chain (<see cref="Threshold"/> against [0, 1] values, the
/// original v1 behaviour) or an HDR chain (against unclamped linear values, with a
/// <see cref="ToneMapEffect"/> compressing the composited result afterwards). Pass a
/// <see cref="RenderTargetFormat.Rgba16F"/> <c>format</c> in an HDR chain so its own bright-pass
/// and blur buffers don't clamp away the over-1.0 values the threshold pass extracted.
/// </remarks>
public sealed class BloomEffect : IPostProcessEffect
{
    private static readonly string VertexSource = EmbeddedShaderSource.Load("PostProcessQuad.vert");
    private static readonly string ThresholdFragmentSource = EmbeddedShaderSource.Load(
        "BloomThreshold.frag"
    );
    private static readonly string BlurFragmentSource = EmbeddedShaderSource.Load("BloomBlur.frag");
    private static readonly string CompositeFragmentSource = EmbeddedShaderSource.Load(
        "BloomComposite.frag"
    );

    private readonly GL _gl;
    private readonly Shader _thresholdShader;
    private readonly Shader _blurShader;
    private readonly Shader _compositeShader;

    // Bright-pass and blur ping-pong targets, kept at half the stack's resolution — bloom is a
    // low-frequency effect, so this halves fill cost with no visible loss of quality, same
    // reasoning IblPrefilter applies to its convolution resolutions.
    private RenderTarget _bright;
    private RenderTarget _blurA;
    private RenderTarget _blurB;

    public bool Enabled { get; set; } = true;

    /// <summary>Brightness (max channel) above which a pixel starts contributing to the bloom.</summary>
    public float Threshold { get; set; } = 1f;

    /// <summary>Width of the soft transition above <see cref="Threshold"/> before a pixel fully contributes.</summary>
    public float SoftKnee { get; set; } = 0.5f;

    /// <summary>Number of horizontal+vertical blur pass pairs. Higher = softer, more expensive glow.</summary>
    public int BlurIterations { get; set; } = 5;

    /// <summary>Strength the blurred bloom texture is added back at during composite.</summary>
    public float Intensity { get; set; } = 1f;

    /// <param name="gl">The window's OpenGL context.</param>
    /// <param name="width">Initial stack width in pixels; the internal buffers are allocated at half this.</param>
    /// <param name="height">Initial stack height in pixels; the internal buffers are allocated at half this.</param>
    /// <param name="format">
    /// Colour format for the internal bright-pass/blur buffers. Match whatever
    /// <see cref="PostProcessStack"/> allocated its own targets as (<see cref="PostProcessStack.HdrEnabled"/>
    /// via <see cref="PostProcessPlanner.SelectSceneFormat"/>) — an HDR chain needs
    /// <see cref="RenderTargetFormat.Rgba16F"/> here too, or values above 1.0 clamp away inside
    /// bloom's own buffers before they ever reach the composite. Defaults to
    /// <see cref="RenderTargetFormat.Rgba8"/>, the original LDR behaviour.
    /// </param>
    public BloomEffect(
        GL gl,
        int width,
        int height,
        RenderTargetFormat format = RenderTargetFormat.Rgba8
    )
    {
        _gl = gl;
        _thresholdShader = new Shader(gl, VertexSource, ThresholdFragmentSource);
        _blurShader = new Shader(gl, VertexSource, BlurFragmentSource);
        _compositeShader = new Shader(gl, VertexSource, CompositeFragmentSource);

        var (w, h) = HalfResolution(width, height);
        _bright = new RenderTarget(gl, w, h, hasDepth: false, format);
        _blurA = new RenderTarget(gl, w, h, hasDepth: false, format);
        _blurB = new RenderTarget(gl, w, h, hasDepth: false, format);
    }

    private static (int Width, int Height) HalfResolution(int width, int height) =>
        (Math.Max(width / 2, 1), Math.Max(height / 2, 1));

    public void Resize(int width, int height)
    {
        var (w, h) = HalfResolution(width, height);
        _bright.Resize(w, h);
        _blurA.Resize(w, h);
        _blurB.Resize(w, h);
    }

    public void Apply(
        uint sourceColorTexture,
        uint destinationFramebuffer,
        int width,
        int height,
        FullscreenQuad quad
    )
    {
        DrawFullscreen(
            _thresholdShader,
            _bright.Fbo,
            _bright.Width,
            _bright.Height,
            quad,
            () =>
            {
                BindTexture(sourceColorTexture, TextureUnit.Texture0);
                _thresholdShader.SetUniformInt("uSource", 0);
                _thresholdShader.SetUniformFloat("uThreshold", Threshold);
                _thresholdShader.SetUniformFloat("uSoftKnee", SoftKnee);
            }
        );

        var currentSource = _bright.ColorTexture;
        var currentDest = _blurA;
        var horizontal = true;
        var totalPasses = Math.Max(BlurIterations, 1) * 2;

        for (var i = 0; i < totalPasses; i++)
        {
            var dest = currentDest;
            var source = currentSource;
            var isHorizontal = horizontal;

            DrawFullscreen(
                _blurShader,
                dest.Fbo,
                dest.Width,
                dest.Height,
                quad,
                () =>
                {
                    BindTexture(source, TextureUnit.Texture0);
                    _blurShader.SetUniformInt("uSource", 0);
                    _blurShader.SetUniformVec2(
                        "uTexelSize",
                        new Vector2(1f / dest.Width, 1f / dest.Height)
                    );
                    _blurShader.SetUniformInt("uHorizontal", isHorizontal ? 1 : 0);
                }
            );

            currentSource = dest.ColorTexture;
            currentDest = ReferenceEquals(currentDest, _blurA) ? _blurB : _blurA;
            horizontal = !horizontal;
        }

        var bloomTexture = currentSource;
        DrawFullscreen(
            _compositeShader,
            destinationFramebuffer,
            width,
            height,
            quad,
            () =>
            {
                BindTexture(sourceColorTexture, TextureUnit.Texture0);
                BindTexture(bloomTexture, TextureUnit.Texture1);
                _compositeShader.SetUniformInt("uScene", 0);
                _compositeShader.SetUniformInt("uBloom", 1);
                _compositeShader.SetUniformFloat("uIntensity", Intensity);
            }
        );
    }

    private void DrawFullscreen(
        Shader shader,
        uint fbo,
        int width,
        int height,
        FullscreenQuad quad,
        Action bindUniforms
    )
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);

        shader.Bind();
        bindUniforms();
        quad.Draw();
        shader.Unbind();
    }

    private void BindTexture(uint texture, TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
    }

    public void Dispose()
    {
        _thresholdShader.Dispose();
        _blurShader.Dispose();
        _compositeShader.Dispose();
        _bright.Dispose();
        _blurA.Dispose();
        _blurB.Dispose();
    }
}

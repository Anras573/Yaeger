using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Renders the scene into an offscreen <see cref="RenderTarget"/>, then runs it through a chain of
/// <see cref="IPostProcessEffect"/> passes — ping-ponging between two offscreen targets, per
/// <see cref="PostProcessPlanner"/> — before the final pass writes to the window's backbuffer.
/// Scene render systems (<see cref="Systems.UnifiedRenderSystem"/>, <see cref="Systems.MeshRenderSystem"/>)
/// need no changes: they simply render while <see cref="Render"/> already has the scene target
/// bound. Render UI/inspector overlays *after* <see cref="Render"/> so they land on the backbuffer
/// unaffected by the effect chain.
/// </summary>
/// <remarks>
/// Setting <see cref="Enabled"/> to <c>false</c> (or leaving <see cref="Effects"/> empty/all
/// disabled) renders the scene straight to whatever framebuffer is already bound — identical to
/// not using a stack at all — so wiring this in is safe without changing existing behaviour.
/// </remarks>
public sealed class PostProcessStack : IDisposable
{
    private static readonly string BlitVertexSource = EmbeddedShaderSource.Load(
        "PostProcessQuad.vert"
    );
    private static readonly string BlitFragmentSource = EmbeddedShaderSource.Load(
        "PostProcessBlit.frag"
    );

    private readonly GL _gl;
    private readonly RenderTarget _scene;
    private readonly RenderTarget _pingPongA;
    private readonly RenderTarget _pingPongB;
    private readonly FullscreenQuad _quad;
    private readonly Shader _blitShader;
    private readonly List<int> _enabledIndices = new();

    private int _width;
    private int _height;

    /// <summary>The effect chain, run in list order. Add/remove/toggle <c>Enabled</c> at runtime.</summary>
    public List<IPostProcessEffect> Effects { get; } = new();

    /// <summary>
    /// Whole-stack toggle. When false, <see cref="Render"/> calls <c>renderScene</c> directly
    /// against whatever framebuffer is already bound (the backbuffer, in the normal render loop)
    /// and skips the offscreen target and effect chain entirely.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <param name="gl">The window's OpenGL context.</param>
    /// <param name="width">Initial target width in pixels — typically the window's client size.</param>
    /// <param name="height">Initial target height in pixels.</param>
    /// <param name="sceneHasDepth">
    /// Whether the offscreen target the scene renders into needs a depth attachment: true for a
    /// 3D scene (<see cref="Systems.MeshRenderSystem"/> depth-tests against it), false for a
    /// depth-free 2D scene.
    /// </param>
    public PostProcessStack(GL gl, int width, int height, bool sceneHasDepth = true)
    {
        _gl = gl;
        _width = Math.Max(width, 1);
        _height = Math.Max(height, 1);

        _scene = new RenderTarget(gl, _width, _height, sceneHasDepth);
        _pingPongA = new RenderTarget(gl, _width, _height, hasDepth: false);
        _pingPongB = new RenderTarget(gl, _width, _height, hasDepth: false);
        _quad = new FullscreenQuad(gl);
        _blitShader = new Shader(gl, BlitVertexSource, BlitFragmentSource);
    }

    /// <summary>
    /// Resizes the offscreen scene/ping-pong targets, and every effect's own targets, to a new
    /// window size. Call from <see cref="Windowing.Window.OnResize"/>. Ignored for a non-positive
    /// size (e.g. a minimize event), so the previous, still-valid targets are kept.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        _width = width;
        _height = height;
        _scene.Resize(width, height);
        _pingPongA.Resize(width, height);
        _pingPongB.Resize(width, height);

        foreach (var effect in Effects)
            effect.Resize(width, height);
    }

    /// <summary>
    /// Renders one frame: <paramref name="renderScene"/> draws into an offscreen target (already
    /// bound beforehand), then every enabled effect in <see cref="Effects"/> runs in order per
    /// <see cref="PostProcessPlanner.Plan"/>, and the final pass writes to the backbuffer.
    /// </summary>
    public void Render(Action renderScene)
    {
        ArgumentNullException.ThrowIfNull(renderScene);

        if (!Enabled)
        {
            renderScene();
            return;
        }

        _scene.Bind();
        renderScene();

        _enabledIndices.Clear();
        for (var i = 0; i < Effects.Count; i++)
        {
            if (Effects[i].Enabled)
                _enabledIndices.Add(i);
        }

        var passes = PostProcessPlanner.Plan(_enabledIndices);

        if (passes.Count == 0)
        {
            BlitToBackbuffer(_scene.ColorTexture);
            return;
        }

        foreach (var pass in passes)
        {
            var sourceTexture = ResolveTexture(pass.Source);
            var destinationFbo =
                pass.Destination == PostProcessSurface.Backbuffer
                    ? 0u
                    : ResolveTarget(pass.Destination).Fbo;

            Effects[pass.EffectIndex].Apply(sourceTexture, destinationFbo, _width, _height, _quad);
        }
    }

    private void BlitToBackbuffer(uint sourceTexture)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);

        _blitShader.Bind();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _blitShader.SetUniformInt("uSource", 0);
        _quad.Draw();
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _blitShader.Unbind();
    }

    private RenderTarget ResolveTarget(PostProcessSurface surface) =>
        surface switch
        {
            PostProcessSurface.Scene => _scene,
            PostProcessSurface.PingPongA => _pingPongA,
            PostProcessSurface.PingPongB => _pingPongB,
            _ => throw new ArgumentOutOfRangeException(
                nameof(surface),
                surface,
                "Not a bindable offscreen render target."
            ),
        };

    private uint ResolveTexture(PostProcessSurface surface) => ResolveTarget(surface).ColorTexture;

    /// <summary>
    /// Disposes the stack's own offscreen targets, quad, and blit shader. Does not dispose
    /// <see cref="Effects"/> — the caller owns whatever it constructed and added, same as
    /// <see cref="Systems.MeshRenderSystem"/> not owning the <see cref="ShadowMapRenderer"/> passed to it.
    /// </summary>
    public void Dispose()
    {
        _scene.Dispose();
        _pingPongA.Dispose();
        _pingPongB.Dispose();
        _quad.Dispose();
        _blitShader.Dispose();
    }
}

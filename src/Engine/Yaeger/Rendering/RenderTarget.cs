using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// An offscreen framebuffer with a colour texture and an optional depth texture, sized to a
/// caller-chosen resolution and recreated in place on <see cref="Resize"/>. Follows
/// <see cref="ShadowMapRenderer"/>'s pattern of a renderer owning its own FBO/texture handles.
/// Used by <see cref="PostProcessStack"/> for the scene target and the two ping-pong targets the
/// effect chain alternates through. The colour attachment's <see cref="RenderTargetFormat"/> is
/// fixed at construction and preserved across <see cref="Resize"/>.
/// </summary>
public sealed class RenderTarget : IDisposable
{
    private readonly GL _gl;
    private readonly bool _hasDepth;
    private readonly RenderTargetFormat _format;
    private uint _fbo;
    private uint _colorTexture;
    private uint _depthTexture;

    /// <summary>Current width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Current height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Handle of the framebuffer object. Bind directly for low-level control (see <see cref="IPostProcessEffect"/>).</summary>
    public uint Fbo => _fbo;

    /// <summary>Handle of the colour attachment, sampled by whatever pass reads this target's output.</summary>
    public uint ColorTexture => _colorTexture;

    public RenderTarget(
        GL gl,
        int width,
        int height,
        bool hasDepth,
        RenderTargetFormat format = RenderTargetFormat.Rgba8
    )
    {
        _gl = gl;
        _hasDepth = hasDepth;
        _format = format;
        Width = Math.Max(width, 1);
        Height = Math.Max(height, 1);
        Create();
    }

    /// <summary>Binds this target's framebuffer and sets the viewport to its current size.</summary>
    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    /// <summary>
    /// Recreates the backing textures/framebuffer at the new size. A no-op when the size hasn't
    /// actually changed, or when either dimension is non-positive (e.g. a minimize event), so the
    /// previous, still-valid resources are kept rather than leaked into a degenerate 0x0 target.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        if (width == Width && height == Height)
            return;

        Width = width;
        Height = height;
        DestroyGlResources();
        Create();
    }

    private unsafe void Create()
    {
        _gl.ActiveTexture(TextureUnit.Texture0);

        var (internalFormat, pixelType) =
            _format == RenderTargetFormat.Rgba16F
                ? (InternalFormat.Rgba16f, PixelType.Float)
                : (InternalFormat.Rgba8, PixelType.UnsignedByte);

        _colorTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
        _gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            (int)internalFormat,
            (uint)Width,
            (uint)Height,
            0,
            PixelFormat.Rgba,
            pixelType,
            null
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _colorTexture,
            0
        );

        if (_hasDepth)
        {
            _depthTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _depthTexture);
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.DepthComponent24,
                (uint)Width,
                (uint)Height,
                0,
                PixelFormat.DepthComponent,
                PixelType.UnsignedInt,
                null
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)GLEnum.Nearest
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Nearest
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.ClampToEdge
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.ClampToEdge
            );
            _gl.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D,
                _depthTexture,
                0
            );
        }

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"RenderTarget framebuffer incomplete: {status}");

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void DestroyGlResources()
    {
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteTexture(_colorTexture);
        if (_hasDepth)
            _gl.DeleteTexture(_depthTexture);
    }

    public void Dispose() => DestroyGlResources();
}

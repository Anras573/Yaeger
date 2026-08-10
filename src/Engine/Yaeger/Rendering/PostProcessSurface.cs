namespace Yaeger.Rendering;

/// <summary>Identifies one of the surfaces a post-processing pass can read from or write to.</summary>
public enum PostProcessSurface
{
    /// <summary>The offscreen target the scene itself was rendered into.</summary>
    Scene,

    /// <summary>The first of two ping-pong targets the effect chain alternates through.</summary>
    PingPongA,

    /// <summary>The second of two ping-pong targets the effect chain alternates through.</summary>
    PingPongB,

    /// <summary>The window's default framebuffer — always the last pass's destination.</summary>
    Backbuffer,
}

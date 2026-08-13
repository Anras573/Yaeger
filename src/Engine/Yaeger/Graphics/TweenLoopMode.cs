namespace Yaeger.Graphics;

/// <summary>
/// How a <see cref="Tween"/> behaves once its progress reaches the end of <see cref="Tween.Duration"/>.
/// </summary>
public enum TweenLoopMode
{
    /// <summary>Plays from <c>From</c> to <c>To</c> once, then holds at <c>To</c> and sets <see cref="Tween.IsFinished"/>.</summary>
    Once,

    /// <summary>Wraps back to <c>From</c> and repeats indefinitely; never finishes.</summary>
    Loop,

    /// <summary>Alternates <c>From</c>→<c>To</c>→<c>From</c> indefinitely; never finishes.</summary>
    PingPong,
}

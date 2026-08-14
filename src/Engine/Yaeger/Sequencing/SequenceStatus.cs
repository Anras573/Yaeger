namespace Yaeger.Sequencing;

/// <summary>Playback state of a sequence started via <see cref="Systems.SequenceSystem.Play"/>.</summary>
public enum SequenceStatus
{
    /// <summary>Advancing normally each <see cref="Systems.SequenceSystem.Update"/> call.</summary>
    Running,

    /// <summary>Frozen by <see cref="Systems.SequenceSystem.Pause"/>; <c>Update</c> skips it entirely.</summary>
    Paused,

    /// <summary>
    /// Reached the end of its steps, was stopped via <see cref="Systems.SequenceSystem.Stop"/>, or
    /// was fast-forwarded via <see cref="Systems.SequenceSystem.SkipToEnd"/>. Terminal — a finished
    /// sequence never resumes.
    /// </summary>
    Finished,
}

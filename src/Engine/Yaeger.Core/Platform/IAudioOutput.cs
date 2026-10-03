using Yaeger.Audio;

namespace Yaeger.Platform;

/// <summary>
/// Opaque handle to a sound decoded fully into memory by <see cref="IAudioOutput.LoadAsync"/>.
/// Handles are only meaningful to the <see cref="IAudioOutput"/> that issued them.
/// </summary>
public readonly record struct SoundHandle(int Id)
{
    /// <summary>A handle that refers to no sound; playing it is a no-op.</summary>
    public static SoundHandle None => default;

    /// <summary>Whether this handle was issued by a successful load.</summary>
    public bool IsValid => Id > 0;
}

/// <summary>
/// Master/music/SFX volume groups. A sound's audible gain is
/// <c>own gain × Master × (group volume)</c>, and changing a volume affects sounds that are
/// already playing. <see cref="Yaeger.Audio.AudioMixer"/> is the reference implementation.
/// </summary>
public interface IAudioMixer
{
    /// <summary>Multiplier applied on top of every group. Clamped to [0, 1]. Defaults to 1.</summary>
    float MasterVolume { get; set; }

    /// <summary>Multiplier for <see cref="AudioGroup.Music"/>. Clamped to [0, 1]. Defaults to 1.</summary>
    float MusicVolume { get; set; }

    /// <summary>Multiplier for <see cref="AudioGroup.Sfx"/>. Clamped to [0, 1]. Defaults to 1.</summary>
    float SfxVolume { get; set; }
}

/// <summary>
/// A long track streamed from <see cref="IAudioOutput.OpenStream"/> rather than decoded fully.
/// Always part of <see cref="AudioGroup.Music"/>. Dispose to release it.
/// </summary>
public interface IMusicStream : IDisposable
{
    /// <summary>Starts or resumes playback.</summary>
    void Play();

    /// <summary>Pauses playback; <see cref="Play"/> resumes from the same position.</summary>
    void Pause();

    /// <summary>Stops playback and rewinds to the start.</summary>
    void Stop();

    /// <summary>Whether the track restarts when it reaches the end. Defaults to <c>false</c>.</summary>
    bool Looping { get; set; }

    /// <summary>
    /// This stream's own logical volume, clamped to [0, 1], on top of the mixer. Ramp it each
    /// frame to crossfade between two streams.
    /// </summary>
    float Gain { get; set; }
}

/// <summary>
/// The small audio surface games actually call, implemented by the native OpenAL runtime and by
/// the browser's WebAudio runtime so the same gameplay code plays sound on both.
/// </summary>
/// <remarks>
/// Asset paths are resolved by the runtime (native: <c>AssetPath</c>; browser: relative to the
/// page's base URI). Call <see cref="Update"/> once per frame. Browsers block audio until the
/// first user gesture: sounds played before then are dropped and music started before then
/// begins on the first interaction.
/// </remarks>
public interface IAudioOutput
{
    /// <summary>Loads and fully decodes a short sound (SFX).</summary>
    Task<SoundHandle> LoadAsync(string path);

    /// <summary>Fire-and-forget playback of a loaded sound.</summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="gain">This one-shot's own logical volume, clamped to [0, 1].</param>
    /// <param name="pitch">Playback-rate multiplier; 1 is unchanged.</param>
    /// <param name="group">The mixer group the sound belongs to.</param>
    void Play(
        SoundHandle sound,
        float gain = 1f,
        float pitch = 1f,
        AudioGroup group = AudioGroup.Sfx
    );

    /// <summary>Opens a long track for streaming playback; it starts stopped.</summary>
    IMusicStream OpenStream(string path);

    /// <summary>The master/music/SFX volume mixer.</summary>
    IAudioMixer Mixer { get; }

    /// <summary>Per-frame housekeeping (feeding streams, recycling voices).</summary>
    void Update(float deltaTime);
}

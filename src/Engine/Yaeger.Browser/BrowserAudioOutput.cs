using Yaeger.Audio;
using Yaeger.Browser.Interop;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IAudioOutput"/> over WebAudio, backed by the "yaeger-browser" JavaScript module.
/// Requires <see cref="YaegerBrowser.InitializeAsync"/> to have completed.
/// </summary>
/// <remarks>
/// <para>
/// Browsers block audio until the first user gesture. The JS side unlocks on the first
/// <c>pointerdown</c>/<c>keydown</c>/touch, without touching the autoplay-gated APIs earlier, so
/// no autoplay warning is logged: loading works immediately, <see cref="Play"/> calls made before
/// the first interaction are dropped, and an <see cref="IMusicStream"/> started before it begins
/// on the first interaction.
/// </para>
/// <para>
/// SFX are fetched and fully decoded; each sound is capped at <c>maxVoicesPerSound</c>
/// simultaneous plays (the oldest is stopped first). Music streams through an
/// <c>&lt;audio&gt;</c> element so long tracks aren't decoded into memory.
/// </para>
/// <para>
/// Ogg Vorbis isn't reliably decodable in Safari. A path ending in <c>.ogg</c> is replaced by
/// a sibling <c>.m4a</c> (or else <c>.mp3</c>) of the same basename when the browser can't play
/// Ogg, so ship those alongside; any other extension is loaded as given.
/// </para>
/// </remarks>
public sealed class BrowserAudioOutput : IAudioOutput
{
    /// <summary>Default for <c>maxVoicesPerSound</c>.</summary>
    public const int DefaultMaxVoicesPerSound = 8;

    private readonly AudioMixer _mixer = new();

    public BrowserAudioOutput(int maxVoicesPerSound = DefaultMaxVoicesPerSound)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxVoicesPerSound, 1);
        JsInterop.AudioInit(maxVoicesPerSound);
        _mixer.VolumeChanged += PushVolumes;
        PushVolumes();
    }

    public IAudioMixer Mixer => _mixer;

    public async Task<SoundHandle> LoadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new SoundHandle(await JsInterop.AudioLoad(path));
    }

    public void Play(
        SoundHandle sound,
        float gain = 1f,
        float pitch = 1f,
        AudioGroup group = AudioGroup.Sfx
    )
    {
        if (!sound.IsValid)
            return;
        JsInterop.AudioPlay(sound.Id, Math.Clamp(gain, 0f, 1f), pitch, (int)group);
    }

    public IMusicStream OpenStream(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new BrowserMusicStream(JsInterop.AudioOpenStream(path));
    }

    /// <summary>Nothing to pump: the browser feeds and recycles audio itself.</summary>
    public void Update(float deltaTime) { }

    private void PushVolumes() =>
        JsInterop.AudioSetVolumes(_mixer.MasterVolume, _mixer.MusicVolume, _mixer.SfxVolume);

    private sealed class BrowserMusicStream(int id) : IMusicStream
    {
        private bool _looping;
        private float _gain = 1f;
        private bool _disposed;

        public bool Looping
        {
            get => _looping;
            set
            {
                _looping = value;
                if (!_disposed)
                    JsInterop.AudioStreamSetLooping(id, value);
            }
        }

        public float Gain
        {
            get => _gain;
            set
            {
                _gain = Math.Clamp(value, 0f, 1f);
                if (!_disposed)
                    JsInterop.AudioStreamSetGain(id, _gain);
            }
        }

        public void Play()
        {
            if (!_disposed)
                JsInterop.AudioStreamPlay(id);
        }

        public void Pause()
        {
            if (!_disposed)
                JsInterop.AudioStreamPause(id);
        }

        public void Stop()
        {
            if (!_disposed)
                JsInterop.AudioStreamStop(id);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            JsInterop.AudioStreamDispose(id);
        }
    }
}

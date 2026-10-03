using Silk.NET.OpenAL;
using Yaeger.Platform;

namespace Yaeger.Audio;

/// <summary>
/// <see cref="IAudioOutput"/> over the native OpenAL runtime: loads <see cref="SoundBuffer"/>s,
/// plays one-shots through a fixed <see cref="OneShotVoicePool"/>, and streams music with
/// <see cref="StreamingSoundSource"/>. Everything is listener-relative (non-positional); use
/// <c>AudioSystem</c> for positional sound. Does not own the <see cref="AudioContext"/>.
/// </summary>
public sealed class NativeAudioOutput : IAudioOutput, IDisposable
{
    private readonly AudioContext _context;
    private readonly Dictionary<int, SoundBuffer> _buffers = new();
    private readonly OneShotVoicePool _voicePool;
    private readonly SoundSource?[] _voices;
    private readonly List<NativeMusicStream> _streams = new();
    private int _nextHandle = 1;
    private bool _disposed;

    public NativeAudioOutput(
        AudioContext context,
        int voiceBudget = 32,
        VoiceStealPolicy stealPolicy = VoiceStealPolicy.Quietest
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _voicePool = new OneShotVoicePool(voiceBudget, stealPolicy);
        _voices = new SoundSource?[voiceBudget];
    }

    public IAudioMixer Mixer => _context.Mixer;

    public Task<SoundHandle> LoadAsync(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        // OpenAL buffers are created on the calling thread, so this completes synchronously.
        try
        {
            var handle = new SoundHandle(_nextHandle++);
            _buffers[handle.Id] = SoundBuffer.FromFile(_context, path);
            return Task.FromResult(handle);
        }
        catch (Exception ex)
        {
            return Task.FromException<SoundHandle>(ex);
        }
    }

    public void Play(
        SoundHandle sound,
        float gain = 1f,
        float pitch = 1f,
        AudioGroup group = AudioGroup.Sfx
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_buffers.TryGetValue(sound.Id, out var buffer))
            return;

        gain = Math.Clamp(gain, 0f, 1f);
        var allocation = _voicePool.Acquire(new VoiceRequest(gain, 0f));
        if (allocation.Kind == VoiceAllocationKind.Dropped)
            return;

        var source = _voices[allocation.SlotIndex] ??= SoundSource.Create(_context, group);
        if (allocation.Kind == VoiceAllocationKind.Stolen)
            source.Stop();

        source.Group = group;
        source.Gain = gain;
        source.Pitch = pitch;
        source.Looping = false;
        source.SetBuffer(buffer);
        source.Play();
    }

    public IMusicStream OpenStream(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = new NativeMusicStream(
            StreamingSoundSource.FromFile(_context, path, AudioGroup.Music),
            this
        );
        _streams.Add(stream);
        return stream;
    }

    public void Update(float deltaTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _voicePool.Tick(deltaTime);
        for (var i = 0; i < _voices.Length; i++)
        {
            if (_voicePool.IsInUse(i) && _voices[i]!.GetState() != SourceState.Playing)
                _voicePool.Release(i);
        }

        foreach (var stream in _streams)
            stream.Pump();
    }

    private void Forget(NativeMusicStream stream) => _streams.Remove(stream);

    /// <summary>Releases every voice, stream and loaded buffer this output owns.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var stream in _streams.ToArray())
            stream.Dispose();
        foreach (var voice in _voices)
            voice?.Dispose();
        foreach (var buffer in _buffers.Values)
            buffer.Dispose();
        _buffers.Clear();
    }

    private sealed class NativeMusicStream(StreamingSoundSource source, NativeAudioOutput owner)
        : IMusicStream
    {
        private bool _disposed;

        public bool Looping
        {
            get => source.Looping;
            set => source.Looping = value;
        }

        public float Gain
        {
            get => source.Gain;
            set => source.Gain = value;
        }

        public void Play() => source.Play();

        public void Pause() => source.Pause();

        public void Stop() => source.Stop();

        internal void Pump()
        {
            if (!_disposed)
                source.Update();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            owner.Forget(this);
            source.Dispose();
        }
    }
}

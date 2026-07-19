using NVorbis;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Streams an OGG Vorbis file through a small ring of OpenAL buffers instead of decoding the
/// whole track into memory up front — the right choice for background music, where a fully
/// decoded multi-minute stereo track would otherwise sit in memory at ~10 MB/minute.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Update"/> regularly (e.g. once per frame) from the update loop — it checks
/// how many queued buffers OpenAL has finished playing, decodes the next chunk of the stream
/// into each one, and re-queues it. The ring holds a few hundred milliseconds of buffered audio
/// ahead of playback, so calling <see cref="Update"/> at typical frame rates leaves a large
/// margin before the source could ever run dry.
/// </para>
/// <para>
/// <see cref="Looping"/> makes the stream seek back to the start the moment it runs out of
/// samples to decode, mid-chunk if necessary, so the loop point never produces a silent gap or
/// an extra buffer boundary click beyond what the source material itself contains.
/// </para>
/// <para>
/// Only mono and stereo streams are supported, matching <see cref="SoundBuffer"/>. Like
/// <see cref="SoundSource"/>, <see cref="Gain"/> is this source's own logical volume; the value
/// actually sent to OpenAL also factors in <see cref="AudioContext.Mixer"/>'s multiplier for
/// this source's <see cref="AudioGroup"/>, kept in sync automatically as mixer volumes change.
/// </para>
/// </remarks>
public sealed class StreamingSoundSource : IDisposable
{
    private const int BufferCount = 4;
    private const int FramesPerBuffer = 8192;

    private readonly AL _al;
    private readonly uint _sourceId;
    private readonly uint[] _bufferIds;
    private readonly VorbisReader _reader;
    private readonly BufferFormat _format;
    private readonly float[] _scratch;
    private readonly MemoryStream _pcmScratch = new();
    private readonly AudioMixer _mixer;
    private readonly AudioGroup _group;
    private float _gain = 1f;
    private bool _looping;
    private bool _disposed;

    private StreamingSoundSource(
        AL al,
        uint sourceId,
        uint[] bufferIds,
        VorbisReader reader,
        BufferFormat format,
        AudioMixer mixer,
        AudioGroup group
    )
    {
        _al = al;
        _sourceId = sourceId;
        _bufferIds = bufferIds;
        _reader = reader;
        _format = format;
        _scratch = new float[FramesPerBuffer * reader.Channels];
        _mixer = mixer;
        _group = group;
        _mixer.VolumeChanged += PushGain;
    }

    /// <summary>
    /// Opens <paramref name="filePath"/> for streaming playback and queues its first buffers.
    /// </summary>
    /// <param name="context">The audio context.</param>
    /// <param name="filePath">Path to an OGG Vorbis file.</param>
    /// <param name="group">
    /// The volume group this source belongs to, for <see cref="AudioContext.Mixer"/>. Defaults
    /// to <see cref="AudioGroup.Music"/>, the primary use case for streaming.
    /// </param>
    /// <exception cref="NotSupportedException">Thrown for anything other than mono or stereo.</exception>
    public static StreamingSoundSource FromFile(
        AudioContext context,
        string filePath,
        AudioGroup group = AudioGroup.Music
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(filePath);

        var resolvedPath = AssetPath.Resolve(filePath);
        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException(
                $"Audio file not found. Requested path: {filePath}, resolved path: {resolvedPath}",
                resolvedPath
            );

        var reader = new VorbisReader(resolvedPath);
        BufferFormat format;
        try
        {
            format = OggVorbisLoader.ResolveFormat(reader.Channels);
        }
        catch
        {
            reader.Dispose();
            throw;
        }

        var al = context.Al;
        uint sourceId = 0;
        var bufferIds = new uint[BufferCount];
        try
        {
            sourceId = al.GenSource();
            for (var i = 0; i < BufferCount; i++)
                bufferIds[i] = al.GenBuffer();

            var source = new StreamingSoundSource(
                al,
                sourceId,
                bufferIds,
                reader,
                format,
                context.Mixer,
                group
            );
            source.PushGain();
            source.FillInitialBuffers();
            return source;
        }
        catch
        {
            foreach (var bufferId in bufferIds)
            {
                if (bufferId != 0)
                    al.DeleteBuffer(bufferId);
            }

            if (sourceId != 0)
                al.DeleteSource(sourceId);

            reader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Feeds newly-processed (played) buffers with the next chunk of decoded audio and re-queues
    /// them. Call this regularly (e.g. once per frame) while the source is playing.
    /// </summary>
    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersProcessed, out int processed);
        if (processed <= 0)
            return;

        var unqueued = new uint[processed];
        _al.SourceUnqueueBuffers(_sourceId, unqueued);

        var refilled = new List<uint>(processed);
        foreach (var bufferId in unqueued)
        {
            if (TryDecodeNextChunk(bufferId))
                refilled.Add(bufferId);
            // Otherwise: nothing left to decode (end of stream, not looping) — leave this
            // buffer unqueued; the source drains and stops naturally once the rest finish.
        }

        if (refilled.Count > 0)
            _al.SourceQueueBuffers(_sourceId, refilled.ToArray());
    }

    /// <summary>Starts (or resumes) playback.</summary>
    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourcePlay(_sourceId);
    }

    /// <summary>Pauses playback; <see cref="Play"/> resumes from the same position.</summary>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourcePause(_sourceId);
    }

    /// <summary>
    /// Stops playback and rewinds to the start of the stream, so a subsequent <see cref="Play"/>
    /// restarts the track from the beginning.
    /// </summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _al.SourceStop(_sourceId);
        UnqueueAll();
        _reader.SamplePosition = 0;
        FillInitialBuffers();
    }

    /// <summary>Gets the current playback state of the source.</summary>
    public SourceState GetState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
        return (SourceState)state;
    }

    /// <summary>
    /// Gets or sets whether the stream restarts from the beginning when it runs out of samples,
    /// instead of letting playback drain and stop. Defaults to <c>false</c>.
    /// </summary>
    public bool Looping
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _looping;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _looping = value;
        }
    }

    /// <summary>
    /// Gets or sets this source's own gain/volume (0.0 to 1.0), independent of
    /// <see cref="AudioContext.Mixer"/> — see the type-level remarks.
    /// </summary>
    public float Gain
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _gain;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _gain = Math.Clamp(value, 0f, 1f);
            PushGain();
        }
    }

    private void PushGain()
    {
        if (_disposed)
            return;

        var effective = _gain * _mixer.GetGroupMultiplier(_group);
        _al.SetSourceProperty(_sourceId, SourceFloat.Gain, Math.Clamp(effective, 0f, 1f));
    }

    private void FillInitialBuffers()
    {
        var queued = new List<uint>(BufferCount);
        foreach (var bufferId in _bufferIds)
        {
            if (!TryDecodeNextChunk(bufferId))
                break; // stream shorter than the whole ring — fine, just queue what we decoded.
            queued.Add(bufferId);
        }

        if (queued.Count > 0)
            _al.SourceQueueBuffers(_sourceId, queued.ToArray());
    }

    private void UnqueueAll()
    {
        _al.GetSourceProperty(_sourceId, GetSourceInteger.BuffersQueued, out int queuedCount);
        if (queuedCount <= 0)
            return;

        var toUnqueue = new uint[queuedCount];
        _al.SourceUnqueueBuffers(_sourceId, toUnqueue);
    }

    /// <summary>
    /// Decodes the next chunk of the stream into <paramref name="bufferId"/>. Returns
    /// <c>false</c> (leaving the buffer's contents untouched) when there's nothing left to
    /// decode — end of stream, and not looping.
    /// </summary>
    private bool TryDecodeNextChunk(uint bufferId)
    {
        var samplesRead = _reader.ReadSamples(_scratch, 0, _scratch.Length);

        if (samplesRead == 0)
        {
            if (!_looping)
                return false;

            _reader.SamplePosition = 0;
            samplesRead = _reader.ReadSamples(_scratch, 0, _scratch.Length);
            if (samplesRead == 0)
                return false; // pathological: empty stream even right after seeking to start.
        }

        _pcmScratch.SetLength(0);
        OggVorbisLoader.WritePcm16(_pcmScratch, _scratch.AsSpan(0, samplesRead));

        var pcm = _pcmScratch.GetBuffer();
        var pcmLength = (int)_pcmScratch.Length;
        unsafe
        {
            fixed (byte* pcmPtr = pcm)
            {
                _al.BufferData(bufferId, _format, pcmPtr, pcmLength, _reader.SampleRate);
            }
        }

        return true;
    }

    /// <summary>
    /// Releases the underlying OpenAL source and buffers, and disposes the OGG decoder.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _mixer.VolumeChanged -= PushGain;
        GC.SuppressFinalize(this);

        _al.SourceStop(_sourceId);
        UnqueueAll();
        _al.DeleteSource(_sourceId);
        foreach (var bufferId in _bufferIds)
            _al.DeleteBuffer(bufferId);

        _reader.Dispose();
        _pcmScratch.Dispose();
    }
}

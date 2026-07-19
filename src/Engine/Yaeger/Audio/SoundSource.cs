using System.Numerics;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Represents an audio source that can play sound buffers.
/// </summary>
/// <remarks>
/// <see cref="Gain"/> is the source's own logical volume; the value actually sent to OpenAL is
/// <c>Gain * AudioContext.Mixer</c>'s multiplier for this source's <see cref="AudioGroup"/>, kept
/// in sync automatically whenever the mixer's volumes change (see
/// <see cref="AudioMixer.VolumeChanged"/>) — so changing e.g. music volume at runtime affects
/// already-playing sources immediately, without touching them directly. Because of this, the
/// <see cref="Gain"/> getter returns the logical value you set, not whatever OpenAL currently
/// reports (which reflects the mixed value).
/// </remarks>
public sealed class SoundSource : IDisposable
{
    private readonly AL _al;
    private readonly uint _sourceId;
    private readonly AudioMixer _mixer;
    private readonly AudioGroup _group;
    private float _gain = 1f;
    private bool _disposed;

    private SoundSource(AL al, uint sourceId, AudioMixer mixer, AudioGroup group)
    {
        _al = al;
        _sourceId = sourceId;
        _mixer = mixer;
        _group = group;
        _mixer.VolumeChanged += PushGain;
    }

    /// <summary>
    /// Gets the OpenAL source ID.
    /// </summary>
    public uint SourceId
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            return _sourceId;
        }
    }

    /// <summary>
    /// Creates a new sound source.
    /// </summary>
    /// <param name="context">The audio context.</param>
    /// <param name="group">
    /// The volume group this source belongs to, for <see cref="AudioContext.Mixer"/>. Defaults
    /// to <see cref="AudioGroup.Sfx"/>.
    /// </param>
    /// <returns>A new SoundSource instance.</returns>
    public static SoundSource Create(AudioContext context, AudioGroup group = AudioGroup.Sfx)
    {
        ArgumentNullException.ThrowIfNull(context);

        var al = context.Al;
        uint sourceId = 0;

        try
        {
            sourceId = al.GenSource();
            var source = new SoundSource(al, sourceId, context.Mixer, group);
            source.PushGain();
            return source;
        }
        catch
        {
            if (sourceId != 0)
            {
                try
                {
                    al.DeleteSource(sourceId);
                }
                catch
                {
                    // Ignore cleanup errors; original exception will be rethrown.
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Sets the buffer to be played by this source.
    /// </summary>
    /// <param name="buffer">The sound buffer to play.</param>
    public void SetBuffer(SoundBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);

        int bufferId;
        try
        {
            bufferId = (int)buffer.BufferId;
        }
        catch (ObjectDisposedException)
        {
            throw new ObjectDisposedException(
                nameof(SoundBuffer),
                "Cannot set a disposed buffer on the sound source."
            );
        }

        _al.SetSourceProperty(_sourceId, SourceInteger.Buffer, bufferId);
    }

    /// <summary>
    /// Starts playing the sound.
    /// </summary>
    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourcePlay(_sourceId);
    }

    /// <summary>
    /// Pauses the sound.
    /// </summary>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourcePause(_sourceId);
    }

    /// <summary>
    /// Stops the sound.
    /// </summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourceStop(_sourceId);
    }

    /// <summary>
    /// Gets the current playback state of the source.
    /// </summary>
    public SourceState GetState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.GetSourceProperty(_sourceId, GetSourceInteger.SourceState, out int state);
        return (SourceState)state;
    }

    /// <summary>
    /// Gets or sets whether the sound should loop.
    /// </summary>
    public bool Looping
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.GetSourceProperty(_sourceId, SourceBoolean.Looping, out bool value);
            return value;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.SetSourceProperty(_sourceId, SourceBoolean.Looping, value);
        }
    }

    /// <summary>
    /// Gets or sets the pitch multiplier (1.0 is normal pitch).
    /// </summary>
    public float Pitch
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.GetSourceProperty(_sourceId, SourceFloat.Pitch, out float value);
            return value;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.SetSourceProperty(_sourceId, SourceFloat.Pitch, value);
        }
    }

    /// <summary>
    /// Gets or sets this source's own gain/volume (0.0 to 1.0), independent of
    /// <see cref="AudioContext.Mixer"/>. Values outside this range are automatically clamped.
    /// The value actually sent to OpenAL is this multiplied by the mixer's gain for this
    /// source's <see cref="AudioGroup"/> — see the type-level remarks.
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

    /// <summary>
    /// Recomputes this source's effective gain (<see cref="Gain"/> × the mixer's multiplier for
    /// this source's <see cref="AudioGroup"/>) and sends it to OpenAL. Called automatically
    /// whenever <see cref="Gain"/> is set or the mixer's volumes change.
    /// </summary>
    private void PushGain()
    {
        if (_disposed)
            return;

        var effective = _gain * _mixer.GetGroupMultiplier(_group);
        _al.SetSourceProperty(_sourceId, SourceFloat.Gain, Math.Clamp(effective, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the position of the sound source in 3D space.
    /// </summary>
    public Vector3 Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.GetSourceProperty(_sourceId, SourceVector3.Position, out var value);
            return value;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.SetSourceProperty(_sourceId, SourceVector3.Position, value);
        }
    }

    /// <summary>
    /// Gets or sets the velocity of the sound source in 3D space (for Doppler effect).
    /// </summary>
    public Vector3 Velocity
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.GetSourceProperty(_sourceId, SourceVector3.Velocity, out var value);
            return value;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.SetSourceProperty(_sourceId, SourceVector3.Velocity, value);
        }
    }

    /// <summary>
    /// Releases the underlying OpenAL sound source and marks this <see cref="SoundSource"/> as disposed.
    /// </summary>
    /// <remarks>
    /// After calling this method, the instance should not be used anymore. Subsequent calls have no effect.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _mixer.VolumeChanged -= PushGain;
        System.GC.SuppressFinalize(this);
        _al.DeleteSource(_sourceId);
    }
}

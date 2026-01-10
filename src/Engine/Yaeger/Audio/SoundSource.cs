using System.Numerics;

using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Represents an audio source that can play sound buffers.
/// </summary>
public sealed class SoundSource : IDisposable
{
    private readonly AL _al;
    private readonly uint _sourceId;
    private bool _disposed;

    private SoundSource(AL al, uint sourceId)
    {
        _al = al;
        _sourceId = sourceId;
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
    /// <returns>A new SoundSource instance.</returns>
    public static SoundSource Create(AudioContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var al = context.Al;
        uint sourceId = 0;

        try
        {
            sourceId = al.GenSource();
            return new SoundSource(al, sourceId);
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
            throw new ObjectDisposedException(nameof(SoundBuffer), "Cannot set a disposed buffer on the sound source.");
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
    /// Gets or sets the gain/volume (0.0 to 1.0).
    /// Values outside this range will be automatically clamped to the valid range.
    /// </summary>
    public float Gain
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.GetSourceProperty(_sourceId, SourceFloat.Gain, out float value);
            return value;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _al.SetSourceProperty(_sourceId, SourceFloat.Gain, Math.Clamp(value, 0f, 1f));
        }
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
        System.GC.SuppressFinalize(this);
        _al.DeleteSource(_sourceId);
    }
}
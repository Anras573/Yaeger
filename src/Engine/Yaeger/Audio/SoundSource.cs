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
    public uint SourceId => _sourceId;

    /// <summary>
    /// Creates a new sound source.
    /// </summary>
    /// <param name="context">The audio context.</param>
    /// <returns>A new SoundSource instance.</returns>
    public static SoundSource Create(AudioContext context)
    {
        var sourceId = context.Al.GenSource();
        return new SoundSource(context.Al, sourceId);
    }

    /// <summary>
    /// Sets the buffer to be played by this source.
    /// </summary>
    /// <param name="buffer">The sound buffer to play.</param>
    public void SetBuffer(SoundBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);
        _al.SetSourceProperty(_sourceId, SourceInteger.Buffer, (int)buffer.BufferId);
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
            _al.GetSourceProperty(_sourceId, SourceBoolean.Looping, out bool value);
            return value;
        }
        set => _al.SetSourceProperty(_sourceId, SourceBoolean.Looping, value);
    }

    /// <summary>
    /// Gets or sets the pitch multiplier (1.0 is normal pitch).
    /// </summary>
    public float Pitch
    {
        get
        {
            _al.GetSourceProperty(_sourceId, SourceFloat.Pitch, out float value);
            return value;
        }
        set => _al.SetSourceProperty(_sourceId, SourceFloat.Pitch, value);
    }

    /// <summary>
    /// Gets or sets the gain/volume (0.0 to 1.0).
    /// </summary>
    public float Gain
    {
        get
        {
            _al.GetSourceProperty(_sourceId, SourceFloat.Gain, out float value);
            return value;
        }
        set => _al.SetSourceProperty(_sourceId, SourceFloat.Gain, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the position of the sound source in 3D space.
    /// </summary>
    public Vector3 Position
    {
        get
        {
            _al.GetSourceProperty(_sourceId, SourceVector3.Position, out var value);
            return value;
        }
        set => _al.SetSourceProperty(_sourceId, SourceVector3.Position, value);
    }

    /// <summary>
    /// Gets or sets the velocity of the sound source in 3D space (for doppler effect).
    /// </summary>
    public Vector3 Velocity
    {
        get
        {
            _al.GetSourceProperty(_sourceId, SourceVector3.Velocity, out var value);
            return value;
        }
        set => _al.SetSourceProperty(_sourceId, SourceVector3.Velocity, value);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _al.DeleteSource(_sourceId);
        _disposed = true;
    }
}
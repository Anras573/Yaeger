using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Manages the OpenAL audio context for the engine.
/// </summary>
public sealed class AudioContext : IDisposable
{
    private readonly AL? _al;
    private readonly ALContext? _alc;
    private readonly nint _device;
    private readonly nint _context;
    private DistanceModel _distanceModel = DistanceModel.InverseDistanceClamped;
    private bool _disposed;

    private readonly IAudioBackend _backend;

    private unsafe AudioContext(AL al, ALContext alc, Device* device, Context* context)
    {
        _al = al;
        _alc = alc;
        _device = (nint)device;
        _context = (nint)context;
        _backend = new SilkAudioBackend(al);
    }

    private AudioContext()
    {
        _backend = new NullAudioBackend();
        IsSilent = true;
    }

    /// <summary>
    /// <c>true</c> for a context created by <see cref="CreateSilent"/>: it accepts every call and
    /// reports plausible state, but produces no sound. Games can ignore this and run unchanged.
    /// </summary>
    public bool IsSilent { get; }

    /// <summary>
    /// Gets the audio backend (real OpenAL, or the silent no-op one).
    /// </summary>
    public IAudioBackend Al
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            return _backend;
        }
    }

    /// <summary>
    /// The master/music/SFX volume mixer shared by every <see cref="SoundSource"/> and
    /// <see cref="StreamingSoundSource"/> created through this context.
    /// </summary>
    public AudioMixer Mixer { get; } = new();

    /// <summary>
    /// The OpenAL distance model applied context-wide to every source's distance attenuation,
    /// on top of each source's own reference distance / max distance / rolloff factor (see
    /// <see cref="SoundSource.ReferenceDistance"/>). Defaults to
    /// <see cref="DistanceModel.InverseDistanceClamped"/>, matching OpenAL's own default.
    /// </summary>
    public DistanceModel DistanceModel
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            return _distanceModel;
        }
        set
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            _distanceModel = value;
            _backend.SetDistanceModel(value);
        }
    }

    /// <summary>
    /// Creates a context with no audio device behind it: every operation succeeds and does
    /// nothing audible. Used by <c>Window</c> when no device is available or audio is disabled.
    /// </summary>
    public static AudioContext CreateSilent() => new();

    /// <summary>
    /// Creates and initializes a new audio context.
    /// </summary>
    /// <returns>A new AudioContext instance.</returns>
    public static unsafe AudioContext Create()
    {
        var al = AL.GetApi(true);
        var alc = ALContext.GetApi(true);

        Device* device = null;
        Context* context = null;

        try
        {
            device = alc.OpenDevice("");
            if (device == null)
            {
                throw new InvalidOperationException("Failed to open audio device");
            }

            context = alc.CreateContext(device, null);
            if (context == null)
            {
                alc.CloseDevice(device);
                throw new InvalidOperationException("Failed to create audio context");
            }

            alc.MakeContextCurrent(context);
            al.DistanceModel(DistanceModel.InverseDistanceClamped);

            return new AudioContext(al, alc, device, context);
        }
        catch
        {
            if (context != null)
            {
                alc.DestroyContext(context);
            }

            if (device != null)
            {
                alc.CloseDevice(device);
            }

            al.Dispose();
            alc.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Releases the OpenAL audio context, device, and associated resources.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times; subsequent calls after the first
    /// have no effect. All cleanup steps are attempted, and if any step throws an
    /// exception, the first such exception is rethrown after all cleanup attempts
    /// have completed. The method also suppresses finalization for this instance.
    /// </remarks>
    public unsafe void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        System.Exception? firstException = null;

        void Try(System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                if (firstException is null)
                {
                    firstException = ex;
                }
            }
        }

        if (_alc is not null && _al is not null)
        {
            Try(() => _alc.MakeContextCurrent(null));
            Try(() => _alc.DestroyContext((Context*)_context));
            Try(() => _alc.CloseDevice((Device*)_device));
            Try(() => _al.Dispose());
            Try(() => _alc.Dispose());
        }

        System.GC.SuppressFinalize(this);

        if (firstException is not null)
        {
            System.Diagnostics.Trace.WriteLine(
                firstException,
                "Error while disposing AudioContext."
            );
        }
    }
}

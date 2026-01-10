using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Manages the OpenAL audio context for the engine.
/// </summary>
public sealed class AudioContext : IDisposable
{
    private readonly AL _al;
    private readonly ALContext _alc;
    private readonly nint _device;
    private readonly nint _context;
    private bool _disposed;

    private unsafe AudioContext(AL al, ALContext alc, Device* device, Context* context)
    {
        _al = al;
        _alc = alc;
        _device = (nint)device;
        _context = (nint)context;
    }

    /// <summary>
    /// Gets the OpenAL instance.
    /// </summary>
    public AL Al
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            return _al;
        }
    }

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

        Try(() => _alc.MakeContextCurrent(null));
        Try(() => _alc.DestroyContext((Context*)_context));
        Try(() => _alc.CloseDevice((Device*)_device));
        Try(() => _al.Dispose());
        Try(() => _alc.Dispose());

        System.GC.SuppressFinalize(this);

        if (firstException is not null)
        {
            throw firstException;
        }
    }
}
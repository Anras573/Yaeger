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
    public AL Al => _al;

    /// <summary>
    /// Creates and initializes a new audio context.
    /// </summary>
    /// <returns>A new AudioContext instance.</returns>
    public static unsafe AudioContext Create()
    {
        var al = AL.GetApi(true);
        var alc = ALContext.GetApi(true);

        var device = alc.OpenDevice("");
        if (device == null)
        {
            throw new InvalidOperationException("Failed to open audio device");
        }

        var context = alc.CreateContext(device, null);
        if (context == null)
        {
            alc.CloseDevice(device);
            throw new InvalidOperationException("Failed to create audio context");
        }

        alc.MakeContextCurrent(context);

        return new AudioContext(al, alc, device, context);
    }

    public unsafe void Dispose()
    {
        if (_disposed)
            return;

        _alc.MakeContextCurrent(null);
        _alc.DestroyContext((Context*)_context);
        _alc.CloseDevice((Device*)_device);
        _al.Dispose();
        _alc.Dispose();
        _disposed = true;
    }
}
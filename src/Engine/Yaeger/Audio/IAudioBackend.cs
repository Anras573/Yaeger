using System.Numerics;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// The slice of the OpenAL API the engine's audio classes use. <see cref="AudioContext"/> exposes
/// one: a thin wrapper over the real OpenAL when an audio device is available, or a no-op
/// implementation that "works" (state round-trips, nothing is audible) when it is not or audio
/// was disabled. Silk.NET's <c>AL</c> class has no virtual methods, so this seam is what makes a
/// silent fake possible.
/// </summary>
public interface IAudioBackend
{
    uint GenBuffer();
    void DeleteBuffer(uint buffer);
    unsafe void BufferData(uint buffer, BufferFormat format, void* data, int size, int frequency);

    uint GenSource();
    void DeleteSource(uint source);
    void SourcePlay(uint source);
    void SourcePause(uint source);
    void SourceStop(uint source);
    void SourceQueueBuffers(uint source, uint[] buffers);
    void SourceUnqueueBuffers(uint source, uint[] buffers);

    void SetSourceProperty(uint source, SourceBoolean property, bool value);
    void SetSourceProperty(uint source, SourceInteger property, int value);
    void SetSourceProperty(uint source, SourceFloat property, float value);
    void SetSourceProperty(uint source, SourceVector3 property, Vector3 value);
    void GetSourceProperty(uint source, SourceBoolean property, out bool value);
    void GetSourceProperty(uint source, GetSourceInteger property, out int value);
    void GetSourceProperty(uint source, SourceFloat property, out float value);
    void GetSourceProperty(uint source, SourceVector3 property, out Vector3 value);

    void SetListenerProperty(ListenerVector3 property, Vector3 value);
    unsafe void SetListenerProperty(ListenerFloatArray property, float* values);

    void SetDistanceModel(DistanceModel model);
}

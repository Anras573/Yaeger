using System.Numerics;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>Forwards every call to the real OpenAL through Silk.NET.</summary>
internal sealed class SilkAudioBackend(AL al) : IAudioBackend
{
    public uint GenBuffer() => al.GenBuffer();

    public void DeleteBuffer(uint buffer) => al.DeleteBuffer(buffer);

    public unsafe void BufferData(
        uint buffer,
        BufferFormat format,
        void* data,
        int size,
        int frequency
    ) => al.BufferData(buffer, format, data, size, frequency);

    public uint GenSource() => al.GenSource();

    public void DeleteSource(uint source) => al.DeleteSource(source);

    public void SourcePlay(uint source) => al.SourcePlay(source);

    public void SourcePause(uint source) => al.SourcePause(source);

    public void SourceStop(uint source) => al.SourceStop(source);

    public void SourceQueueBuffers(uint source, uint[] buffers) =>
        al.SourceQueueBuffers(source, buffers);

    public void SourceUnqueueBuffers(uint source, uint[] buffers) =>
        al.SourceUnqueueBuffers(source, buffers);

    public void SetSourceProperty(uint source, SourceBoolean property, bool value) =>
        al.SetSourceProperty(source, property, value);

    public void SetSourceProperty(uint source, SourceInteger property, int value) =>
        al.SetSourceProperty(source, property, value);

    public void SetSourceProperty(uint source, SourceFloat property, float value) =>
        al.SetSourceProperty(source, property, value);

    public void SetSourceProperty(uint source, SourceVector3 property, Vector3 value) =>
        al.SetSourceProperty(source, property, value);

    public void GetSourceProperty(uint source, SourceBoolean property, out bool value) =>
        al.GetSourceProperty(source, property, out value);

    public void GetSourceProperty(uint source, GetSourceInteger property, out int value) =>
        al.GetSourceProperty(source, property, out value);

    public void GetSourceProperty(uint source, SourceFloat property, out float value) =>
        al.GetSourceProperty(source, property, out value);

    public void GetSourceProperty(uint source, SourceVector3 property, out Vector3 value) =>
        al.GetSourceProperty(source, property, out value);

    public void SetListenerProperty(ListenerVector3 property, Vector3 value) =>
        al.SetListenerProperty(property, value);

    public unsafe void SetListenerProperty(ListenerFloatArray property, float* values) =>
        al.SetListenerProperty(property, values);

    public void SetDistanceModel(DistanceModel model) => al.DistanceModel(model);
}

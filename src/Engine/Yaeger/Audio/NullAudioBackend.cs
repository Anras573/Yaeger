using System.Numerics;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// A silent <see cref="IAudioBackend"/> used when no audio device is available (CI, containers,
/// VMs) or audio is disabled. It behaves like a real backend as far as callers can tell:
/// generated ids are unique, set properties read back, and queued/unqueued buffers are tracked.
/// Nothing is ever audible, and playback completes immediately: playing a non-looping source
/// leaves it <see cref="SourceState.Stopped"/> straight away (so one-shot voice pools recycle),
/// while a looping source stays <see cref="SourceState.Playing"/> until stopped.
/// </summary>
internal sealed class NullAudioBackend : IAudioBackend
{
    private sealed class Source
    {
        public SourceState State = SourceState.Initial;
        public bool Relative;
        public bool Looping;
        public int Buffer;
        public float Gain = 1f;
        public float Pitch = 1f;
        public float ReferenceDistance = 1f;
        public float MaxDistance = float.MaxValue;
        public float RolloffFactor = 1f;
        public Vector3 Position;
        public Vector3 Velocity;
        public readonly Queue<uint> Queued = new();
    }

    private readonly Dictionary<uint, Source> _sources = [];
    private uint _nextId = 1;

    private Source Get(uint id) =>
        _sources.TryGetValue(id, out var source)
            ? source
            : throw new InvalidOperationException($"Unknown audio source id {id}.");

    public uint GenBuffer() => _nextId++;

    public void DeleteBuffer(uint buffer) { }

    public unsafe void BufferData(
        uint buffer,
        BufferFormat format,
        void* data,
        int size,
        int frequency
    ) { }

    public uint GenSource()
    {
        var id = _nextId++;
        _sources[id] = new Source();
        return id;
    }

    public void DeleteSource(uint source) => _sources.Remove(source);

    public void SourcePlay(uint source)
    {
        var s = Get(source);
        s.State = s.Looping ? SourceState.Playing : SourceState.Stopped;
    }

    public void SourcePause(uint source)
    {
        var s = Get(source);
        if (s.State == SourceState.Playing)
            s.State = SourceState.Paused;
    }

    public void SourceStop(uint source)
    {
        var s = Get(source);
        if (s.State != SourceState.Initial)
            s.State = SourceState.Stopped;
    }

    public void SourceQueueBuffers(uint source, uint[] buffers)
    {
        var s = Get(source);
        foreach (var buffer in buffers)
            s.Queued.Enqueue(buffer);
    }

    public void SourceUnqueueBuffers(uint source, uint[] buffers)
    {
        var s = Get(source);
        for (var i = 0; i < buffers.Length && s.Queued.Count > 0; i++)
            buffers[i] = s.Queued.Dequeue();
    }

    public void SetSourceProperty(uint source, SourceBoolean property, bool value)
    {
        var s = Get(source);
        switch (property)
        {
            case SourceBoolean.SourceRelative:
                s.Relative = value;
                break;
            case SourceBoolean.Looping:
                s.Looping = value;
                break;
        }
    }

    public void SetSourceProperty(uint source, SourceInteger property, int value)
    {
        if (property == SourceInteger.Buffer)
            Get(source).Buffer = value;
    }

    public void SetSourceProperty(uint source, SourceFloat property, float value)
    {
        var s = Get(source);
        switch (property)
        {
            case SourceFloat.Gain:
                s.Gain = value;
                break;
            case SourceFloat.Pitch:
                s.Pitch = value;
                break;
            case SourceFloat.ReferenceDistance:
                s.ReferenceDistance = value;
                break;
            case SourceFloat.MaxDistance:
                s.MaxDistance = value;
                break;
            case SourceFloat.RolloffFactor:
                s.RolloffFactor = value;
                break;
        }
    }

    public void SetSourceProperty(uint source, SourceVector3 property, Vector3 value)
    {
        var s = Get(source);
        switch (property)
        {
            case SourceVector3.Position:
                s.Position = value;
                break;
            case SourceVector3.Velocity:
                s.Velocity = value;
                break;
        }
    }

    public void GetSourceProperty(uint source, SourceBoolean property, out bool value) =>
        value = property switch
        {
            SourceBoolean.SourceRelative => Get(source).Relative,
            SourceBoolean.Looping => Get(source).Looping,
            _ => false,
        };

    // Queued buffers are never "processed": nothing plays, so a stream simply doesn't advance.
    public void GetSourceProperty(uint source, GetSourceInteger property, out int value) =>
        value = property switch
        {
            GetSourceInteger.SourceState => (int)Get(source).State,
            GetSourceInteger.BuffersQueued => Get(source).Queued.Count,
            _ => 0,
        };

    public void GetSourceProperty(uint source, SourceFloat property, out float value)
    {
        var s = Get(source);
        value = property switch
        {
            SourceFloat.Gain => s.Gain,
            SourceFloat.Pitch => s.Pitch,
            SourceFloat.ReferenceDistance => s.ReferenceDistance,
            SourceFloat.MaxDistance => s.MaxDistance,
            SourceFloat.RolloffFactor => s.RolloffFactor,
            _ => 0f,
        };
    }

    public void GetSourceProperty(uint source, SourceVector3 property, out Vector3 value)
    {
        var s = Get(source);
        value = property switch
        {
            SourceVector3.Position => s.Position,
            SourceVector3.Velocity => s.Velocity,
            _ => Vector3.Zero,
        };
    }

    public void SetListenerProperty(ListenerVector3 property, Vector3 value) { }

    public unsafe void SetListenerProperty(ListenerFloatArray property, float* values) { }

    public void SetDistanceModel(DistanceModel model) { }
}

using System.Numerics;
using Silk.NET.OpenAL;
using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Drives positional audio: syncs every <see cref="AudioSource3D"/> entity's underlying OpenAL
/// source position (and volume/loop/distance parameters) from its <see cref="Transform3D"/> or
/// <see cref="Transform2D"/> each update, and points the OpenAL listener at the scene's active
/// camera — the first <see cref="Camera3D"/> entity (position plus at/up orientation derived from
/// its view matrix via <see cref="AudioSpatialMath.ExtractOrientation"/>), or failing that the
/// first <see cref="Camera2D"/> entity (position mapped onto the Z=0 plane, fixed orientation), or
/// the world origin if neither exists.
/// </summary>
/// <remarks>
/// Owns one <see cref="SoundSource"/> per <see cref="AudioSource3D"/> entity, created lazily the
/// first update an entity is seen and disposed once the entity no longer carries the component
/// (component removed or entity destroyed) — the same "diff and clean up" pattern
/// <c>TilemapColliderSystem</c> uses for its generated colliders. Dispose this system to release
/// every OpenAL source it still owns (e.g. on window shutdown).
/// </remarks>
public sealed class AudioSystem(World world, Yaeger.Audio.AudioContext audioContext)
    : IUpdateSystem,
        IDisposable
{
    private readonly Dictionary<Entity, TrackedSource> _tracked = new();
    private readonly HashSet<Entity> _seen = new();
    private readonly List<Entity> _stale = new();
    private bool _disposed;

    private readonly record struct TrackedSource(SoundSource Source, SoundBuffer? Buffer);

    public void Update(float deltaTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        UpdateListener();
        SyncSources();
    }

    private void UpdateListener()
    {
        var (position, at, up) = ResolveListener();

        var al = audioContext.Al;
        al.SetListenerProperty(ListenerVector3.Position, position);

        Span<float> orientation = [at.X, at.Y, at.Z, up.X, up.Y, up.Z];
        unsafe
        {
            fixed (float* orientationPtr = orientation)
                al.SetListenerProperty(ListenerFloatArray.Orientation, orientationPtr);
        }
    }

    private (Vector3 Position, Vector3 At, Vector3 Up) ResolveListener()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var (at, up) = AudioSpatialMath.ExtractOrientation(camera.ViewMatrix);
            return (camera.Position, at, up);
        }

        foreach (var (_, camera2D) in world.GetStore<Camera2D>().All())
        {
            var (at, up) = AudioSpatialMath.PlaneOrientation;
            return (AudioSpatialMath.ToListenerPlane(camera2D.Position), at, up);
        }

        var (defaultAt, defaultUp) = AudioSpatialMath.PlaneOrientation;
        return (Vector3.Zero, defaultAt, defaultUp);
    }

    private void SyncSources()
    {
        _seen.Clear();

        foreach (
            (Entity entity, AudioSource3D source, Transform3D transform) in world.Query<
                AudioSource3D,
                Transform3D
            >()
        )
        {
            _seen.Add(entity);
            Sync(entity, source, transform.Position);
        }

        // 2D fallback for entities without a Transform3D — an entity carrying both takes the 3D
        // path above and is skipped here via _seen.
        foreach (
            (Entity entity, AudioSource3D source, Transform2D transform) in world.Query<
                AudioSource3D,
                Transform2D
            >()
        )
        {
            if (!_seen.Add(entity))
                continue;
            Sync(entity, source, AudioSpatialMath.ToListenerPlane(transform.Position));
        }

        RemoveStale();
    }

    private void Sync(Entity entity, in AudioSource3D audioSource, Vector3 position)
    {
        if (!_tracked.TryGetValue(entity, out var tracked))
            tracked = new TrackedSource(
                SoundSource.Create(audioContext, AudioGroup.Sfx, listenerRelative: false),
                null
            );

        var source = tracked.Source;
        source.Position = position;
        source.Looping = audioSource.Loop;
        source.Gain = audioSource.Volume;
        source.ReferenceDistance = audioSource.MinDistance;
        source.MaxDistance = audioSource.MaxDistance;
        source.RolloffFactor = audioSource.RolloffFactor;

        if (!ReferenceEquals(tracked.Buffer, audioSource.Buffer))
        {
            // OpenAL rejects changing a playing/paused source's buffer, so stop first — a no-op
            // on a freshly created or already-stopped source.
            source.Stop();
            if (audioSource.Buffer is not null)
            {
                source.SetBuffer(audioSource.Buffer);
                source.Play();
            }
            tracked = new TrackedSource(source, audioSource.Buffer);
        }

        _tracked[entity] = tracked;
    }

    private void RemoveStale()
    {
        _stale.Clear();
        foreach (var entity in _tracked.Keys)
        {
            if (!_seen.Contains(entity))
                _stale.Add(entity);
        }

        foreach (var entity in _stale)
        {
            _tracked[entity].Source.Dispose();
            _tracked.Remove(entity);
        }
    }

    /// <summary>Releases every OpenAL source this system created for a live <see cref="AudioSource3D"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var tracked in _tracked.Values)
            tracked.Source.Dispose();
        _tracked.Clear();
    }
}

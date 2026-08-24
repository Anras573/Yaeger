using System.Diagnostics;
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
/// the world origin if neither exists. Also owns a fixed-size pool of voices for
/// <see cref="PlayOneShot(SoundBuffer, Vector3, float, AudioGroup, int)"/> — fire-and-forget
/// positioned SFX that don't need an entity.
/// </summary>
/// <remarks>
/// Owns one <see cref="SoundSource"/> per <see cref="AudioSource3D"/> entity, created lazily the
/// first update an entity is seen and disposed once the entity no longer carries the component
/// (component removed or entity destroyed) — the same "diff and clean up" pattern
/// <c>TilemapColliderSystem</c> uses for its generated colliders. Dispose this system to release
/// every OpenAL source it still owns (e.g. on window shutdown).
/// </remarks>
public sealed class AudioSystem(
    World world,
    Yaeger.Audio.AudioContext audioContext,
    int oneShotVoiceBudget = 32,
    VoiceStealPolicy oneShotStealPolicy = VoiceStealPolicy.Quietest
) : IUpdateSystem, IDisposable
{
    private readonly Dictionary<Entity, TrackedSource> _tracked = new();
    private readonly HashSet<Entity> _seen = new();
    private readonly List<Entity> _stale = new();
    private readonly OneShotVoicePool _voicePool = new(oneShotVoiceBudget, oneShotStealPolicy);
    private readonly SoundSource?[] _voiceSources = new SoundSource?[oneShotVoiceBudget];
    private Vector3 _listenerPosition;
    private bool _disposed;

    private readonly record struct TrackedSource(SoundSource Source, SoundBuffer? Buffer);

    /// <summary>The fixed number of concurrent one-shot voices this system can play at once.</summary>
    public int OneShotVoiceBudget => _voicePool.Capacity;

    /// <summary>
    /// The policy applied to pick which one-shot voice to steal once <see cref="OneShotVoiceBudget"/>
    /// is exhausted. Swappable at any time.
    /// </summary>
    public VoiceStealPolicy OneShotStealPolicy
    {
        get => _voicePool.Policy;
        set => _voicePool.Policy = value;
    }

    public void Update(float deltaTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        UpdateListener();
        SyncSources();
        UpdateOneShotVoices(deltaTime);
    }

    /// <summary>
    /// Fire-and-forget playback of <paramref name="buffer"/> at a world position — no entity or
    /// cleanup required. Backed by a fixed pool of <see cref="OneShotVoiceBudget"/> voices,
    /// recycled once their playback finishes; once every voice is in use, the next request steals
    /// one per <see cref="OneShotStealPolicy"/> (never a voice whose <paramref name="priority"/>
    /// is lower than its own) or is dropped if none is eligible. A dropped request fails loudly
    /// via <see cref="Debug.Fail(string?)"/> in debug builds and is a silent no-op otherwise —
    /// this is expected, graceful degradation under load, not an error.
    /// </summary>
    /// <param name="buffer">The sound to play.</param>
    /// <param name="position">The world position to play it from.</param>
    /// <param name="gain">This one-shot's own logical volume (0–1), same meaning as <see cref="SoundSource.Gain"/>.</param>
    /// <param name="group">The <see cref="AudioContext.Mixer"/> volume group this one-shot belongs to.</param>
    /// <param name="priority">Higher values are less likely to be stolen — see <see cref="VoiceRequest.Priority"/>.</param>
    public void PlayOneShot(
        SoundBuffer buffer,
        Vector3 position,
        float gain = 1f,
        AudioGroup group = AudioGroup.Sfx,
        int priority = 0
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);

        var distance = Vector3.Distance(_listenerPosition, position);
        var allocation = _voicePool.Acquire(new VoiceRequest(gain, distance, priority));

        if (allocation.Kind == VoiceAllocationKind.Dropped)
        {
            Debug.Fail(
                $"AudioSystem.PlayOneShot: voice budget ({_voicePool.Capacity}) exhausted and no "
                    + $"eligible voice to steal (priority {priority}); dropping this one-shot."
            );
            return;
        }

        var source = _voiceSources[allocation.SlotIndex] ??= SoundSource.Create(
            audioContext,
            group,
            listenerRelative: false
        );

        if (allocation.Kind == VoiceAllocationKind.Stolen)
            source.Stop();

        source.Group = group;
        source.Position = position;
        source.Gain = gain;
        source.Looping = false;
        source.SetBuffer(buffer);
        source.Play();
    }

    /// <summary>
    /// Convenience overload for 2D games: maps <paramref name="position"/> onto the same Z=0
    /// plane <see cref="AudioSource3D"/>'s 2D fallback and a <see cref="Camera2D"/> listener use.
    /// </summary>
    public void PlayOneShot(
        SoundBuffer buffer,
        Vector2 position,
        float gain = 1f,
        AudioGroup group = AudioGroup.Sfx,
        int priority = 0
    ) => PlayOneShot(buffer, AudioSpatialMath.ToListenerPlane(position), gain, group, priority);

    private void UpdateOneShotVoices(float deltaTime)
    {
        _voicePool.Tick(deltaTime);

        for (var i = 0; i < _voiceSources.Length; i++)
        {
            if (!_voicePool.IsInUse(i))
                continue;

            var source = _voiceSources[i];
            if (source is not null && source.GetState() != SourceState.Playing)
                _voicePool.Release(i);
        }
    }

    private void UpdateListener()
    {
        var (position, at, up) = ResolveListener();
        _listenerPosition = position;

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

    /// <summary>
    /// Releases every OpenAL source this system created, both for a live <see cref="AudioSource3D"/>
    /// and for the one-shot voice pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var tracked in _tracked.Values)
            tracked.Source.Dispose();
        _tracked.Clear();

        for (var i = 0; i < _voiceSources.Length; i++)
        {
            _voiceSources[i]?.Dispose();
            _voiceSources[i] = null;
        }
    }
}

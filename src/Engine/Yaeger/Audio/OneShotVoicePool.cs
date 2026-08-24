namespace Yaeger.Audio;

/// <summary>
/// Policy <see cref="OneShotVoicePool"/> applies when a one-shot is requested and every voice
/// slot is already in use.
/// </summary>
public enum VoiceStealPolicy
{
    /// <summary>Steal the in-use voice with the lowest <see cref="VoiceRequest.Gain"/>.</summary>
    Quietest,

    /// <summary>Steal the in-use voice with the greatest <see cref="VoiceRequest.DistanceFromListener"/>.</summary>
    MostDistant,

    /// <summary>Steal the in-use voice that has been playing the longest.</summary>
    Oldest,

    /// <summary>Never steal — drop the incoming one-shot instead.</summary>
    DropNew,
}

/// <summary>A request to play a one-shot sound, as seen by <see cref="OneShotVoicePool"/>.</summary>
/// <param name="Gain">The one-shot's own logical volume, used by <see cref="VoiceStealPolicy.Quietest"/>.</param>
/// <param name="DistanceFromListener">Distance from the listener, used by <see cref="VoiceStealPolicy.MostDistant"/>.</param>
/// <param name="Priority">
/// Higher values are more important. A voice is only ever stolen by a request whose
/// <see cref="Priority"/> is greater than or equal to its own — so a high-priority sound (a
/// scripted key sound) can't be stolen by low-priority ambience.
/// </param>
public readonly record struct VoiceRequest(
    float Gain,
    float DistanceFromListener,
    int Priority = 0
);

/// <summary>How <see cref="OneShotVoicePool.Acquire"/> satisfied (or didn't satisfy) a <see cref="VoiceRequest"/>.</summary>
public enum VoiceAllocationKind
{
    /// <summary>An unused slot was available.</summary>
    FreeSlot,

    /// <summary>Every slot was in use; an existing voice was stolen per the pool's <see cref="VoiceStealPolicy"/>.</summary>
    Stolen,

    /// <summary>Every slot was in use and none could be stolen (see <see cref="VoiceStealPolicy"/>/priority); the request was dropped.</summary>
    Dropped,
}

/// <summary>The result of <see cref="OneShotVoicePool.Acquire"/>.</summary>
/// <param name="Kind">How the request was satisfied.</param>
/// <param name="SlotIndex">The allocated slot index, or -1 when <paramref name="Kind"/> is <see cref="VoiceAllocationKind.Dropped"/>.</param>
public readonly record struct VoiceAllocation(VoiceAllocationKind Kind, int SlotIndex)
{
    public static readonly VoiceAllocation Dropped = new(VoiceAllocationKind.Dropped, -1);
}

/// <summary>
/// Pure CPU-side voice budgeting and stealing logic for one-shot SFX (see
/// <see cref="Yaeger.Systems.AudioSystem.PlayOneShot"/>) — no OpenAL dependency, so it's
/// unit-tested directly, the same split <c>MeshInstanceBatcher</c>/<c>PostProcessPlanner</c> use
/// for their systems.
/// </summary>
/// <remarks>
/// Tracks a fixed number of voice slots (<see cref="Capacity"/>). <see cref="Acquire"/> hands out
/// a free slot if one exists; once every slot is in use, it applies <see cref="Policy"/> to decide
/// whether to steal an existing slot (and which one) or drop the incoming request. The caller
/// (<c>AudioSystem</c>) owns the actual OpenAL <c>SoundSource</c> per slot and is responsible for
/// calling <see cref="Release"/> once that slot's playback finishes, and <see cref="Tick"/> once
/// per update so <see cref="VoiceStealPolicy.Oldest"/> has an accurate age to compare.
/// </remarks>
public sealed class OneShotVoicePool
{
    private struct Slot
    {
        public bool InUse;
        public float Gain;
        public float DistanceFromListener;
        public int Priority;
        public float Age;
    }

    private readonly Slot[] _slots;

    public OneShotVoicePool(int capacity, VoiceStealPolicy policy = VoiceStealPolicy.Quietest)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Voice pool capacity must be positive."
            );

        _slots = new Slot[capacity];
        Policy = policy;
    }

    /// <summary>The fixed number of voice slots this pool manages.</summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// The stealing policy applied once every slot is in use. Swappable at any time, including
    /// mid-game (e.g. a boss fight temporarily favouring <see cref="VoiceStealPolicy.MostDistant"/>).
    /// </summary>
    public VoiceStealPolicy Policy { get; set; }

    /// <summary>Whether <paramref name="slotIndex"/> currently holds a playing voice.</summary>
    public bool IsInUse(int slotIndex) => _slots[slotIndex].InUse;

    /// <summary>
    /// Allocates a slot for <paramref name="request"/>: a free slot if one exists, otherwise a
    /// slot stolen per <see cref="Policy"/>, otherwise <see cref="VoiceAllocation.Dropped"/>.
    /// </summary>
    public VoiceAllocation Acquire(VoiceRequest request)
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].InUse)
                continue;

            Occupy(i, request);
            return new VoiceAllocation(VoiceAllocationKind.FreeSlot, i);
        }

        if (Policy == VoiceStealPolicy.DropNew)
            return VoiceAllocation.Dropped;

        var victim = -1;
        for (var i = 0; i < _slots.Length; i++)
        {
            // A slot playing something more important than the incoming request is never stolen.
            if (_slots[i].Priority > request.Priority)
                continue;

            if (victim == -1 || IsBetterVictim(i, victim))
                victim = i;
        }

        if (victim == -1)
            return VoiceAllocation.Dropped;

        Occupy(victim, request);
        return new VoiceAllocation(VoiceAllocationKind.Stolen, victim);
    }

    /// <summary>Frees <paramref name="slotIndex"/> once its voice has finished playing.</summary>
    public void Release(int slotIndex) => _slots[slotIndex].InUse = false;

    /// <summary>Advances the age of every in-use slot, for <see cref="VoiceStealPolicy.Oldest"/>.</summary>
    public void Tick(float deltaTime)
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].InUse)
                _slots[i].Age += deltaTime;
        }
    }

    private bool IsBetterVictim(int candidate, int current) =>
        Policy switch
        {
            VoiceStealPolicy.Quietest => _slots[candidate].Gain < _slots[current].Gain,
            VoiceStealPolicy.MostDistant => _slots[candidate].DistanceFromListener
                > _slots[current].DistanceFromListener,
            VoiceStealPolicy.Oldest => _slots[candidate].Age > _slots[current].Age,
            _ => false,
        };

    private void Occupy(int index, VoiceRequest request)
    {
        _slots[index] = new Slot
        {
            InUse = true,
            Gain = request.Gain,
            DistanceFromListener = request.DistanceFromListener,
            Priority = request.Priority,
            Age = 0f,
        };
    }
}

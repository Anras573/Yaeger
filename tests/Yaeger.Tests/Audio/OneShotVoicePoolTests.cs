using Yaeger.Audio;

namespace Yaeger.Tests.Audio;

// Pure CPU-side voice budgeting/stealing logic — no OpenAL device needed, unlike the rest of Audio/.
public class OneShotVoicePoolTests
{
    [Fact]
    public void Constructor_NonPositiveCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OneShotVoicePool(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OneShotVoicePool(-1));
    }

    [Fact]
    public void Acquire_ShouldFillFreeSlotsBeforeStealing()
    {
        var pool = new OneShotVoicePool(3);

        var first = pool.Acquire(new VoiceRequest(1f, 0f));
        var second = pool.Acquire(new VoiceRequest(1f, 0f));
        var third = pool.Acquire(new VoiceRequest(1f, 0f));

        Assert.Equal(VoiceAllocationKind.FreeSlot, first.Kind);
        Assert.Equal(VoiceAllocationKind.FreeSlot, second.Kind);
        Assert.Equal(VoiceAllocationKind.FreeSlot, third.Kind);
        Assert.Equal([0, 1, 2], new[] { first.SlotIndex, second.SlotIndex, third.SlotIndex });
    }

    [Fact]
    public void Acquire_QuietestPolicy_ShouldStealLowestGainVoice()
    {
        var pool = new OneShotVoicePool(2, VoiceStealPolicy.Quietest);
        pool.Acquire(new VoiceRequest(Gain: 0.8f, DistanceFromListener: 0f));
        var quiet = pool.Acquire(new VoiceRequest(Gain: 0.1f, DistanceFromListener: 0f));

        var allocation = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(quiet.SlotIndex, allocation.SlotIndex);
    }

    [Fact]
    public void Acquire_MostDistantPolicy_ShouldStealFarthestVoice()
    {
        var pool = new OneShotVoicePool(2, VoiceStealPolicy.MostDistant);
        pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 5f));
        var farthest = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 50f));

        var allocation = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(farthest.SlotIndex, allocation.SlotIndex);
    }

    [Fact]
    public void Acquire_OldestPolicy_ShouldStealVoiceThatHasPlayedLongest()
    {
        var pool = new OneShotVoicePool(2, VoiceStealPolicy.Oldest);
        var oldest = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));
        pool.Tick(1f);
        pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));
        pool.Tick(1f);

        var allocation = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(oldest.SlotIndex, allocation.SlotIndex);
    }

    [Fact]
    public void Acquire_DropNewPolicy_ShouldNeverStealWhenFull()
    {
        var pool = new OneShotVoicePool(1, VoiceStealPolicy.DropNew);
        pool.Acquire(new VoiceRequest(Gain: 0.01f, DistanceFromListener: 1000f));

        var allocation = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));

        Assert.Equal(VoiceAllocation.Dropped, allocation);
    }

    [Fact]
    public void Acquire_HigherPriorityVoice_ShouldNeverBeStolenByLowerPriorityRequest()
    {
        var pool = new OneShotVoicePool(1, VoiceStealPolicy.Quietest);
        pool.Acquire(new VoiceRequest(Gain: 0.01f, DistanceFromListener: 0f, Priority: 10));

        var allocation = pool.Acquire(
            new VoiceRequest(Gain: 1f, DistanceFromListener: 0f, Priority: 0)
        );

        Assert.Equal(VoiceAllocation.Dropped, allocation);
    }

    [Fact]
    public void Acquire_EqualPriority_ShouldBeStealable()
    {
        var pool = new OneShotVoicePool(1, VoiceStealPolicy.Quietest);
        var occupied = pool.Acquire(
            new VoiceRequest(Gain: 0.01f, DistanceFromListener: 0f, Priority: 5)
        );

        var allocation = pool.Acquire(
            new VoiceRequest(Gain: 1f, DistanceFromListener: 0f, Priority: 5)
        );

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(occupied.SlotIndex, allocation.SlotIndex);
    }

    [Fact]
    public void Acquire_HigherPriorityRequest_ShouldStealLowerPriorityVoiceEvenIfLouder()
    {
        var pool = new OneShotVoicePool(1, VoiceStealPolicy.Quietest);
        var occupied = pool.Acquire(
            new VoiceRequest(Gain: 1f, DistanceFromListener: 0f, Priority: 0)
        );

        var allocation = pool.Acquire(
            new VoiceRequest(Gain: 0.01f, DistanceFromListener: 0f, Priority: 10)
        );

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(occupied.SlotIndex, allocation.SlotIndex);
    }

    [Fact]
    public void Release_ShouldFreeSlotForReuse()
    {
        var pool = new OneShotVoicePool(1);
        var allocation = pool.Acquire(new VoiceRequest(1f, 0f));

        pool.Release(allocation.SlotIndex);
        var next = pool.Acquire(new VoiceRequest(1f, 0f));

        Assert.Equal(VoiceAllocationKind.FreeSlot, next.Kind);
        Assert.Equal(allocation.SlotIndex, next.SlotIndex);
    }

    [Fact]
    public void IsInUse_ShouldReflectAllocationAndRelease()
    {
        var pool = new OneShotVoicePool(1);
        Assert.False(pool.IsInUse(0));

        var allocation = pool.Acquire(new VoiceRequest(1f, 0f));
        Assert.True(pool.IsInUse(allocation.SlotIndex));

        pool.Release(allocation.SlotIndex);
        Assert.False(pool.IsInUse(allocation.SlotIndex));
    }

    [Fact]
    public void Policy_ShouldBeSwappableAfterConstruction()
    {
        var pool = new OneShotVoicePool(2, VoiceStealPolicy.Quietest);
        pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 100f));
        var farthest = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 200f));

        pool.Policy = VoiceStealPolicy.MostDistant;
        var allocation = pool.Acquire(new VoiceRequest(Gain: 1f, DistanceFromListener: 0f));

        Assert.Equal(VoiceAllocationKind.Stolen, allocation.Kind);
        Assert.Equal(farthest.SlotIndex, allocation.SlotIndex);
    }
}

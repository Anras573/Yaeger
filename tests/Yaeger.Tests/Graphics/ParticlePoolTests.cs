using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ParticlePoolTests
{
    [Fact]
    public void Constructor_WithNonPositiveCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticlePool(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticlePool(-1));
    }

    [Fact]
    public void TrySpawn_ShouldIncreaseAliveCount()
    {
        var pool = new ParticlePool(4);

        var spawned = pool.TrySpawn(new Vector2(1f, 2f), new Vector2(3f, 4f), 1f);

        Assert.True(spawned);
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(new Vector2(1f, 2f), pool[0].Position);
        Assert.Equal(new Vector2(3f, 4f), pool[0].Velocity);
        Assert.Equal(0f, pool[0].Age);
        Assert.Equal(1f, pool[0].Lifetime);
    }

    [Fact]
    public void TrySpawn_WhenFull_ShouldReturnFalse()
    {
        var pool = new ParticlePool(2);
        Assert.True(pool.TrySpawn(Vector2.Zero, Vector2.Zero, 1f));
        Assert.True(pool.TrySpawn(Vector2.Zero, Vector2.Zero, 1f));

        var spawned = pool.TrySpawn(Vector2.Zero, Vector2.Zero, 1f);

        Assert.False(spawned);
        Assert.Equal(2, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldIntegrateVelocityIntoPosition()
    {
        var pool = new ParticlePool(1);
        pool.TrySpawn(new Vector2(1f, 1f), new Vector2(2f, -4f), 10f);

        pool.Update(0.5f);

        Assert.Equal(2f, pool[0].Position.X, 0.0001f);
        Assert.Equal(-1f, pool[0].Position.Y, 0.0001f);
        Assert.Equal(0.5f, pool[0].Age, 0.0001f);
    }

    [Fact]
    public void Update_ShouldRecycleExpiredParticles()
    {
        var pool = new ParticlePool(3);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 0.1f);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 1.0f);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 0.2f);

        pool.Update(0.5f);

        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(1.0f, pool[0].Lifetime, 0.0001f);
    }

    [Fact]
    public void Update_ShouldAgeAndMoveSwappedInParticleInSameFrame()
    {
        var pool = new ParticlePool(2);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 0.1f);
        pool.TrySpawn(Vector2.Zero, new Vector2(1f, 0f), lifetime: 5f);

        // The first particle expires and the second is swapped into slot 0; the swapped
        // particle must still be aged and integrated during the same update.
        pool.Update(0.5f);

        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(0.5f, pool[0].Age, 0.0001f);
        Assert.Equal(0.5f, pool[0].Position.X, 0.0001f);
    }

    [Fact]
    public void TrySpawn_AfterRecycling_ShouldReuseFreedSlot()
    {
        var pool = new ParticlePool(1);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 0.1f);
        pool.Update(1f);
        Assert.Equal(0, pool.AliveCount);

        var spawned = pool.TrySpawn(Vector2.One, Vector2.Zero, lifetime: 1f);

        Assert.True(spawned);
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(Vector2.One, pool[0].Position);
    }

    [Fact]
    public void Indexer_OutsideAliveRange_ShouldThrow()
    {
        var pool = new ParticlePool(4);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, 1f);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = pool[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = pool[1]);
    }

    [Fact]
    public void NormalizedAge_ShouldReportFractionOfLifetime()
    {
        var pool = new ParticlePool(1);
        pool.TrySpawn(Vector2.Zero, Vector2.Zero, lifetime: 2f);

        pool.Update(0.5f);

        Assert.Equal(0.25f, pool[0].NormalizedAge, 0.0001f);
    }
}

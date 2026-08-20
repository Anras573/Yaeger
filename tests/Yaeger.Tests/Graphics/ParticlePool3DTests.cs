using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ParticlePool3DTests
{
    [Fact]
    public void Constructor_WithNonPositiveCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticlePool3D(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticlePool3D(-1));
    }

    [Fact]
    public void TrySpawn_ShouldIncreaseAliveCount()
    {
        var pool = new ParticlePool3D(4);

        var spawned = pool.TrySpawn(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), 1f);

        Assert.True(spawned);
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(new Vector3(1f, 2f, 3f), pool[0].Position);
        Assert.Equal(new Vector3(4f, 5f, 6f), pool[0].Velocity);
        Assert.Equal(0f, pool[0].Age);
        Assert.Equal(1f, pool[0].Lifetime);
    }

    [Fact]
    public void TrySpawn_WithoutSizeMultiplierOrRotation_ShouldDefaultToNoJitter()
    {
        var pool = new ParticlePool3D(1);

        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 1f);

        Assert.Equal(1f, pool[0].SizeMultiplier);
        Assert.Equal(0f, pool[0].InitialRotation);
    }

    [Fact]
    public void TrySpawn_WithSizeMultiplierAndRotation_ShouldStoreThem()
    {
        var pool = new ParticlePool3D(1);

        pool.TrySpawn(
            Vector3.Zero,
            Vector3.Zero,
            1f,
            sizeMultiplier: 1.5f,
            initialRotation: MathF.PI
        );

        Assert.Equal(1.5f, pool[0].SizeMultiplier);
        Assert.Equal(MathF.PI, pool[0].InitialRotation);
    }

    [Fact]
    public void TrySpawn_WhenFull_ShouldReturnFalse()
    {
        var pool = new ParticlePool3D(2);
        Assert.True(pool.TrySpawn(Vector3.Zero, Vector3.Zero, 1f));
        Assert.True(pool.TrySpawn(Vector3.Zero, Vector3.Zero, 1f));

        var spawned = pool.TrySpawn(Vector3.Zero, Vector3.Zero, 1f);

        Assert.False(spawned);
        Assert.Equal(2, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldIntegrateVelocityIntoPosition()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(new Vector3(1f, 1f, 0f), new Vector3(2f, -4f, 1f), 10f);

        pool.Update(0.5f);

        Assert.Equal(2f, pool[0].Position.X, 0.0001f);
        Assert.Equal(-1f, pool[0].Position.Y, 0.0001f);
        Assert.Equal(0.5f, pool[0].Position.Z, 0.0001f);
        Assert.Equal(0.5f, pool[0].Age, 0.0001f);
    }

    [Fact]
    public void Update_ShouldRecycleExpiredParticles()
    {
        var pool = new ParticlePool3D(3);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 0.1f);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 1.0f);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 0.2f);

        pool.Update(0.5f);

        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(1.0f, pool[0].Lifetime, 0.0001f);
    }

    [Fact]
    public void Update_ShouldAgeAndMoveSwappedInParticleInSameFrame()
    {
        var pool = new ParticlePool3D(2);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 0.1f);
        pool.TrySpawn(Vector3.Zero, new Vector3(1f, 0f, 0f), lifetime: 5f);

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
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 0.1f);
        pool.Update(1f);
        Assert.Equal(0, pool.AliveCount);

        var spawned = pool.TrySpawn(Vector3.One, Vector3.Zero, lifetime: 1f);

        Assert.True(spawned);
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(Vector3.One, pool[0].Position);
    }

    [Fact]
    public void Indexer_OutsideAliveRange_ShouldThrow()
    {
        var pool = new ParticlePool3D(4);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 1f);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = pool[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = pool[1]);
    }

    [Fact]
    public void NormalizedAge_ShouldReportFractionOfLifetime()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, lifetime: 2f);

        pool.Update(0.5f);

        Assert.Equal(0.25f, pool[0].NormalizedAge, 0.0001f);
    }

    // ── Forces: acceleration, drag, turbulence ──────────────────────────────

    [Fact]
    public void Update_WithZeroForces_ShouldMatchExistingConstantVelocityBehaviour()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(new Vector3(1f, 1f, 0f), new Vector3(2f, -4f, 1f), 10f);

        pool.Update(0.5f, Vector3.Zero, 0f, 0f, 1f);

        Assert.Equal(2f, pool[0].Position.X, 0.0001f);
        Assert.Equal(-1f, pool[0].Position.Y, 0.0001f);
        Assert.Equal(0.5f, pool[0].Position.Z, 0.0001f);
        Assert.Equal(new Vector3(2f, -4f, 1f), pool[0].Velocity);
    }

    [Fact]
    public void Update_WithAcceleration_ShouldIntegrateIntoVelocityBeforePosition()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 10f);

        // Semi-implicit Euler: velocity picks up this step's acceleration *before* position
        // integrates, so after one 1s step velocity = accel * dt and position = velocity * dt
        // (accel * dt^2), not the explicit-Euler position = 0 you'd get integrating the
        // pre-step velocity.
        pool.Update(1f, acceleration: new Vector3(0f, 2f, 0f));

        Assert.Equal(new Vector3(0f, 2f, 0f), pool[0].Velocity);
        Assert.Equal(new Vector3(0f, 2f, 0f), pool[0].Position);
    }

    [Fact]
    public void Update_WithUpwardAcceleration_ShouldRiseFasterEachStep()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 10f);

        pool.Update(0.1f, acceleration: new Vector3(0f, 5f, 0f));
        var firstStepHeight = pool[0].Position.Y;
        pool.Update(0.1f, acceleration: new Vector3(0f, 5f, 0f));
        var secondStepGain = pool[0].Position.Y - firstStepHeight;

        Assert.True(secondStepGain > firstStepHeight);
    }

    [Fact]
    public void Update_WithDrag_ShouldDampVelocityTowardZero()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, new Vector3(10f, 0f, 0f), 10f);

        pool.Update(0.1f, drag: 5f);

        Assert.True(pool[0].Velocity.X < 10f);
        Assert.True(pool[0].Velocity.X > 0f);
    }

    [Fact]
    public void Update_WithDrag_ShouldNeverReverseVelocityDirection()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, new Vector3(3f, 0f, 0f), 10f);

        // A large drag * deltaTime would overshoot past zero under a naive linear damping term;
        // exponential decay never crosses zero regardless of how large the product gets.
        pool.Update(1f, drag: 1000f);

        Assert.True(pool[0].Velocity.X >= 0f);
    }

    [Fact]
    public void Update_WithZeroDrag_ShouldNotDampVelocity()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, new Vector3(3f, 0f, 0f), 10f);

        pool.Update(1f, drag: 0f);

        Assert.Equal(3f, pool[0].Velocity.X, 0.0001f);
    }

    [Fact]
    public void Update_WithTurbulence_ShouldPerturbVelocity()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 10f);

        pool.Update(0.1f, turbulence: 5f, turbulenceFrequency: 1f);

        Assert.NotEqual(Vector3.Zero, pool[0].Velocity);
    }

    [Fact]
    public void Update_WithZeroTurbulence_ShouldNotPerturbVelocity()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 10f);

        pool.Update(0.1f, turbulence: 0f);

        Assert.Equal(Vector3.Zero, pool[0].Velocity);
    }

    [Fact]
    public void Update_WithTurbulence_ShouldNotProduceDiscontinuousJumps()
    {
        var pool = new ParticlePool3D(1);
        pool.TrySpawn(Vector3.Zero, Vector3.Zero, 10f);

        Vector3? previousVelocity = null;
        for (var i = 0; i < 50; i++)
        {
            pool.Update(0.02f, turbulence: 1f, turbulenceFrequency: 1f);
            var velocity = pool[0].Velocity;
            if (previousVelocity is { } prev)
                Assert.True(Vector3.Distance(prev, velocity) < 0.5f);
            previousVelocity = velocity;
        }
    }
}

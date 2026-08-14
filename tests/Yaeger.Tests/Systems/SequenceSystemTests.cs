using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Sequencing;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class SequenceSystemTests
{
    // ── Ordering ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_StepsRunInOrder()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .Callback(() => log.Add("a"))
            .Callback(() => log.Add("b"))
            .Callback(() => log.Add("c"))
            .Build();

        var system = new SequenceSystem();
        system.Play(sequence);
        system.Update(0.016f);

        Assert.Equal(["a", "b", "c"], log);
    }

    [Fact]
    public void Update_ChainOfInstantSteps_AllCompleteWithinOneFrame()
    {
        // Zero-duration waits and callbacks shouldn't each cost their own frame — otherwise a
        // sample stringing several together would need several Update calls just to get past them.
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForSeconds(0f)
            .Callback(() => log.Add("first"))
            .WaitForSeconds(0f)
            .Callback(() => log.Add("second"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(0f);

        Assert.Equal(["first", "second"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void Update_WaitForSeconds_SplitsAcrossMultipleFrames()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForSeconds(1f)
            .Callback(() => log.Add("done"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        system.Update(0.5f);
        Assert.Empty(log);
        Assert.False(system.IsFinished(handle));

        system.Update(0.5f);
        Assert.Equal(["done"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void Update_LargeDeltaSpanningMultipleWaits_CarriesLeftoverBudgetForward()
    {
        // A single big frame (e.g. a stall) should still walk through every step it has time for,
        // not just the first one — frame-rate independence, same spirit as TweenSystem.
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForSeconds(0.5f)
            .Callback(() => log.Add("first"))
            .WaitForSeconds(1f)
            .Callback(() => log.Add("second"))
            .WaitForSeconds(10f)
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(2f);

        Assert.Equal(["first", "second"], log);
        Assert.False(system.IsFinished(handle));
    }

    // ── Tween integration ───────────────────────────────────────────────────

    [Fact]
    public void Update_StartTween_AddsTweenComponentToCarrier()
    {
        var world = new World();
        var door = world.CreateEntity("door");
        world.AddComponent(door, new Transform2D(Vector2.Zero));
        var tween = Tween.Create(
            door,
            TweenChannel.Transform2DPosition,
            Vector2.Zero,
            new Vector2(1f, 0f),
            duration: 1f
        );
        var sequence = new SequenceBuilder(world).StartTween(door, tween).Build();

        var system = new SequenceSystem();
        system.Play(sequence);
        system.Update(0f);

        Assert.True(world.TryGetComponent<Tween>(door, out var stored));
        Assert.Equal(1f, stored.Duration);
    }

    [Fact]
    public void Update_WaitForTweenFinished_BlocksUntilTweenSystemMarksItFinished()
    {
        var world = new World();
        var door = world.CreateEntity("door");
        world.AddComponent(door, new Transform2D(Vector2.Zero));
        var tween = Tween.Create(
            door,
            TweenChannel.Transform2DPosition,
            Vector2.Zero,
            new Vector2(1f, 0f),
            duration: 1f
        );
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .StartTween(door, tween)
            .WaitForTweenFinished(door)
            .Callback(() => log.Add("opened"))
            .Build();

        var tweenSystem = new TweenSystem(world);
        var sequenceSystem = new SequenceSystem();
        var handle = sequenceSystem.Play(sequence);

        // Halfway through the tween's duration: the door is still moving, so the sequence must
        // still be waiting.
        sequenceSystem.Update(0.5f);
        tweenSystem.Update(0.5f);
        sequenceSystem.Update(0.001f);
        Assert.Empty(log);
        Assert.False(sequenceSystem.IsFinished(handle));

        // Cross the tween's duration: TweenSystem marks it finished, and the very next sequence
        // Update should observe that and proceed.
        tweenSystem.Update(0.6f);
        sequenceSystem.Update(0.001f);

        Assert.Equal(["opened"], log);
        Assert.True(sequenceSystem.IsFinished(handle));
    }

    [Fact]
    public void Update_WaitForTweenFinished_LoopingTween_NeverCompletesOnItsOwn()
    {
        var world = new World();
        var door = world.CreateEntity("door");
        world.AddComponent(door, new Transform2D(Vector2.Zero));
        var tween = Tween.Create(
            door,
            TweenChannel.Transform2DPosition,
            Vector2.Zero,
            new Vector2(1f, 0f),
            duration: 1f,
            loopMode: TweenLoopMode.Loop
        );
        var sequence = new SequenceBuilder(world)
            .StartTween(door, tween)
            .WaitForTweenFinished(door)
            .Build();

        var tweenSystem = new TweenSystem(world);
        var sequenceSystem = new SequenceSystem();
        var handle = sequenceSystem.Play(sequence);

        sequenceSystem.Update(0f);
        for (var i = 0; i < 20; i++)
        {
            tweenSystem.Update(1f);
            sequenceSystem.Update(1f);
        }

        Assert.False(sequenceSystem.IsFinished(handle));
    }

    // ── WaitUntil ───────────────────────────────────────────────────────────

    [Fact]
    public void Update_WaitUntil_BlocksUntilPredicateReturnsTrue()
    {
        var world = new World();
        var ready = false;
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitUntil(() => ready)
            .Callback(() => log.Add("go"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        system.Update(1f);
        Assert.Empty(log);
        Assert.False(system.IsFinished(handle));

        ready = true;
        system.Update(1f);
        Assert.Equal(["go"], log);
        Assert.True(system.IsFinished(handle));
    }

    // ── Parallel groups ─────────────────────────────────────────────────────

    [Fact]
    public void Update_Parallel_CompletesOnceEveryBranchDoes()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .Parallel(
                branch => branch.WaitForSeconds(1f).Callback(() => log.Add("short")),
                branch => branch.WaitForSeconds(3f).Callback(() => log.Add("long"))
            )
            .Callback(() => log.Add("afterParallel"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        system.Update(1f);
        Assert.Equal(["short"], log);
        Assert.False(system.IsFinished(handle));

        system.Update(2f);
        Assert.Equal(["short", "long", "afterParallel"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void Update_Parallel_EachBranchGetsTheFullIncomingBudgetIndependently()
    {
        // A single large frame should let every branch progress by the full delta, not some
        // fraction split across siblings — otherwise a slower branch would fall further and
        // further behind a faster one every frame instead of finishing on its own schedule.
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .Parallel(
                branch => branch.WaitForSeconds(1f).Callback(() => log.Add("short")),
                branch => branch.WaitForSeconds(1.8f).Callback(() => log.Add("long"))
            )
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(2f);

        Assert.Equal(["short", "long"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void Update_NestedParallelInsideParallelBranch_Completes()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .Parallel(
                branch =>
                    branch.Parallel(
                        inner => inner.WaitForSeconds(1f).Callback(() => log.Add("inner-a")),
                        inner => inner.WaitForSeconds(2f).Callback(() => log.Add("inner-b"))
                    ),
                branch => branch.WaitForSeconds(0.5f).Callback(() => log.Add("outer"))
            )
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(2f);

        // Branches resolve depth-first in the order they were added: the first outer branch (the
        // nested parallel) fully resolves — both its inner branches — before the second outer
        // branch even starts.
        Assert.Equal(["inner-a", "inner-b", "outer"], log);
        Assert.True(system.IsFinished(handle));
    }

    // ── Control surface: pause / resume / stop / skip-to-end ───────────────

    [Fact]
    public void Pause_FreezesProgressUntilResume()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForSeconds(1f)
            .Callback(() => log.Add("done"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        system.Update(0.5f);
        system.Pause(handle);
        system.Update(10f); // large update while paused must be completely ignored
        Assert.Empty(log);
        Assert.Equal(SequenceStatus.Paused, GetStatus(system, handle));

        system.Resume(handle);
        system.Update(0.5f);
        Assert.Equal(["done"], log);
    }

    [Fact]
    public void Pause_UnknownOrFinishedHandle_IsNoOp()
    {
        var system = new SequenceSystem();
        var unknown = new SequenceHandle(999);

        var exception = Record.Exception(() =>
        {
            system.Pause(unknown);
            system.Resume(unknown);
            system.Stop(unknown);
            system.SkipToEnd(unknown);
        });

        Assert.Null(exception);
        Assert.True(system.IsFinished(unknown));
    }

    [Fact]
    public void Stop_HaltsImmediately_NoFurtherStepsRun()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForSeconds(1f)
            .Callback(() => log.Add("should-not-run"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        system.Update(0.5f);
        system.Stop(handle);
        system.Update(1f);

        Assert.Empty(log);
        Assert.True(system.IsFinished(handle));
        Assert.Equal(SequenceStatus.Finished, GetStatus(system, handle));
    }

    [Fact]
    public void SkipToEnd_RunsRemainingActionsButBypassesWaits()
    {
        var world = new World();
        var log = new List<string>();
        var predicateEvaluated = false;
        var sequence = new SequenceBuilder(world)
            .Callback(() => log.Add("first"))
            .WaitForSeconds(60f)
            .WaitUntil(() =>
            {
                predicateEvaluated = true;
                return false;
            })
            .Callback(() => log.Add("second"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(0f); // runs "first", then blocks on the 60s wait

        system.SkipToEnd(handle);

        Assert.Equal(["first", "second"], log);
        Assert.False(predicateEvaluated);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void SkipToEnd_ParallelGroup_RunsEveryBranchsActions()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .Parallel(
                branch => branch.WaitForSeconds(5f).Callback(() => log.Add("a")),
                branch => branch.WaitForSeconds(50f).Callback(() => log.Add("b"))
            )
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(0f);
        system.SkipToEnd(handle);

        Assert.Equal(["a", "b"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void SkipToEnd_ActionAlreadyRunBeforeSkip_DoesNotRunTwice()
    {
        var world = new World();
        var runCount = 0;
        var sequence = new SequenceBuilder(world)
            .Callback(() => runCount++)
            .WaitForSeconds(5f)
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(0f); // callback already fires here

        system.SkipToEnd(handle);

        Assert.Equal(1, runCount);
    }

    // ── Fail-safe behaviour ─────────────────────────────────────────────────

    [Fact]
    public void Update_WaitForTweenFinished_CarrierDestroyedMidSequence_TreatsAsFinishedInsteadOfHanging()
    {
        var world = new World();
        var door = world.CreateEntity("door");
        world.AddComponent(door, new Transform2D(Vector2.Zero));
        var tween = Tween.Create(
            door,
            TweenChannel.Transform2DPosition,
            Vector2.Zero,
            new Vector2(1f, 0f),
            duration: 5f
        );
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .StartTween(door, tween)
            .WaitForTweenFinished(door)
            .Callback(() => log.Add("continued"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);

        var exception = Record.Exception(() =>
        {
            system.Update(0.1f); // starts the tween, begins waiting on it
            world.DestroyEntity(door);
            system.Update(0.1f);
        });

        Assert.Null(exception);
        Assert.Equal(["continued"], log);
        Assert.True(system.IsFinished(handle));
    }

    [Fact]
    public void Update_WaitForTweenFinished_CarrierNeverHadATween_TreatsAsFinishedImmediately()
    {
        var world = new World();
        var carrier = world.CreateEntity();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world)
            .WaitForTweenFinished(carrier)
            .Callback(() => log.Add("continued"))
            .Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(0f);

        Assert.Equal(["continued"], log);
        Assert.True(system.IsFinished(handle));
    }

    // ── Skeletal clip starter ───────────────────────────────────────────────

    [Fact]
    public void Update_StartSkeletalClip_ReplacesAnimationPlayerAndResetsTime()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new AnimationPlayer("idle", loop: true) { Time = 3f });

        var sequence = new SequenceBuilder(world)
            .StartSkeletalClip(entity, "wave", loop: false, speed: 1.5f)
            .Build();

        var system = new SequenceSystem();
        system.Play(sequence);
        system.Update(0f);

        var player = world.GetComponent<AnimationPlayer>(entity);
        Assert.Equal("wave", player.CurrentClip);
        Assert.Equal(0f, player.Time);
        Assert.False(player.Loop);
        Assert.Equal(1.5f, player.Speed);
        Assert.Null(player.PreviousClip);
    }

    // ── Validation and misuse ───────────────────────────────────────────────

    [Fact]
    public void WaitForSeconds_NegativeDuration_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SequenceBuilder(new World()).WaitForSeconds(-1f)
        );

    [Fact]
    public void WaitForSeconds_NonFiniteDuration_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SequenceBuilder(new World()).WaitForSeconds(float.NaN)
        );

    [Fact]
    public void WaitUntil_NullPredicate_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new SequenceBuilder(new World()).WaitUntil(null!)
        );

    [Fact]
    public void Callback_NullAction_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new SequenceBuilder(new World()).Callback(null!)
        );

    [Fact]
    public void Play_NullSequence_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SequenceSystem().Play(null!));

    [Fact]
    public void Update_NegativeDeltaTime_IsNoOp()
    {
        var world = new World();
        var log = new List<string>();
        var sequence = new SequenceBuilder(world).Callback(() => log.Add("ran")).Build();

        var system = new SequenceSystem();
        var handle = system.Play(sequence);
        system.Update(-1f);

        Assert.Empty(log);
        Assert.False(system.IsFinished(handle));
    }

    [Fact]
    public void Play_SameSequencePlayedTwiceConcurrently_ProgressesIndependently()
    {
        var world = new World();
        var sequence = new SequenceBuilder(world).WaitForSeconds(1f).Build();

        var system = new SequenceSystem();
        var first = system.Play(sequence);
        system.Update(0.9f); // first is almost done
        var second = system.Play(sequence); // second starts fresh

        system.Update(0.2f); // first crosses 1s; second is only at 0.2s

        Assert.True(system.IsFinished(first));
        Assert.False(system.IsFinished(second));
    }

    private static SequenceStatus GetStatus(SequenceSystem system, SequenceHandle handle)
    {
        Assert.True(system.TryGetStatus(handle, out var status));
        return status;
    }
}

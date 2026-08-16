using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class CameraRigSystemTests
{
    // ── Pure math (issue #192: deterministic seed, fixed deltas) ────────────

    [Fact]
    public void ApplySmoothing_ZeroSmoothing_SnapsDirectlyToTarget()
    {
        var result = CameraRigSystem.ApplySmoothing(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            smoothing: 0f,
            deltaTime: 1f / 60f
        );

        Assert.Equal(new Vector3(10f, 0f, 0f), result);
    }

    [Fact]
    public void ApplySmoothing_NegativeSmoothing_SnapsDirectlyToTarget()
    {
        var result = CameraRigSystem.ApplySmoothing(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            smoothing: -1f,
            deltaTime: 1f / 60f
        );

        Assert.Equal(new Vector3(10f, 0f, 0f), result);
    }

    [Fact]
    public void ApplySmoothing_PositiveSmoothing_MovesPartwayTowardsTarget()
    {
        var result = CameraRigSystem.ApplySmoothing(
            Vector3.Zero,
            new Vector3(10f, 0f, 0f),
            smoothing: 5f,
            deltaTime: 0.1f
        );

        Assert.True(result.X > 0f, "Expected some movement towards the target");
        Assert.True(result.X < 10f, "Expected not to have fully reached the target in one step");
    }

    [Fact]
    public void DecayTrauma_ReducesByDecayTimesDeltaTime()
    {
        var result = CameraRigSystem.DecayTrauma(trauma: 1f, decay: 2f, deltaTime: 0.1f);

        Assert.Equal(0.8f, result, 4);
    }

    [Fact]
    public void DecayTrauma_NeverGoesBelowZero()
    {
        var result = CameraRigSystem.DecayTrauma(trauma: 0.1f, decay: 5f, deltaTime: 1f);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void DecayTrauma_NeverExceedsOne()
    {
        // A trauma value above 1 shouldn't occur in practice (CameraShake.AddTrauma clamps), but
        // the decay function clamps defensively regardless.
        var result = CameraRigSystem.DecayTrauma(trauma: 1.5f, decay: 0f, deltaTime: 0f);

        Assert.Equal(1f, result);
    }

    [Fact]
    public void ComputeShakeOffset_ZeroTrauma_ReturnsZero()
    {
        var offset = CameraRigSystem.ComputeShakeOffset(
            seed: 42,
            trauma: 0f,
            maxOffset: 0.5f,
            elapsed: 1.23f
        );

        Assert.Equal(Vector3.Zero, offset);
    }

    [Fact]
    public void ComputeShakeOffset_SameInputs_AreDeterministic()
    {
        var first = CameraRigSystem.ComputeShakeOffset(
            seed: 7,
            trauma: 0.6f,
            maxOffset: 0.4f,
            elapsed: 2.5f
        );
        var second = CameraRigSystem.ComputeShakeOffset(
            seed: 7,
            trauma: 0.6f,
            maxOffset: 0.4f,
            elapsed: 2.5f
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeShakeOffset_DifferentSeeds_ProduceDifferentOffsets()
    {
        var a = CameraRigSystem.ComputeShakeOffset(
            seed: 1,
            trauma: 1f,
            maxOffset: 0.5f,
            elapsed: 1f
        );
        var b = CameraRigSystem.ComputeShakeOffset(
            seed: 2,
            trauma: 1f,
            maxOffset: 0.5f,
            elapsed: 1f
        );

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeShakeOffset_AtFullTrauma_EachAxisStaysWithinMaxOffset()
    {
        const float maxOffset = 0.5f;

        // Sample across a range of elapsed times to exercise the noise function broadly.
        for (var t = 0f; t < 20f; t += 0.37f)
        {
            var offset = CameraRigSystem.ComputeShakeOffset(
                seed: 99,
                trauma: 1f,
                maxOffset: maxOffset,
                elapsed: t
            );

            Assert.True(MathF.Abs(offset.X) <= maxOffset + 1e-4f, $"X exceeded bound at t={t}");
            Assert.True(MathF.Abs(offset.Y) <= maxOffset + 1e-4f, $"Y exceeded bound at t={t}");
            Assert.True(MathF.Abs(offset.Z) <= maxOffset + 1e-4f, $"Z exceeded bound at t={t}");
        }
    }

    [Fact]
    public void ComputeShakeOffset_HalfTrauma_IsQuarterAmplitudeOfFullTrauma()
    {
        // trauma^2 scaling: half trauma should shake at 1/4 the amplitude of full trauma, not 1/2.
        var full = CameraRigSystem.ComputeShakeOffset(
            seed: 3,
            trauma: 1f,
            maxOffset: 1f,
            elapsed: 5f
        );
        var half = CameraRigSystem.ComputeShakeOffset(
            seed: 3,
            trauma: 0.5f,
            maxOffset: 1f,
            elapsed: 5f
        );

        Assert.Equal(full.X * 0.25f, half.X, 4);
        Assert.Equal(full.Y * 0.25f, half.Y, 4);
        Assert.Equal(full.Z * 0.25f, half.Z, 4);
    }

    [Fact]
    public void ComputeForward_IdentityRotation_ReturnsNegativeZ()
    {
        var forward = CameraRigSystem.ComputeForward(Quaternion.Identity);

        Assert.Equal(-Vector3.UnitZ, forward);
    }

    [Fact]
    public void ComputeForward_NinetyDegreeYaw_RotatesToNegativeX()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

        var forward = CameraRigSystem.ComputeForward(rotation);

        Assert.Equal(-1f, forward.X, 4);
        Assert.Equal(0f, forward.Y, 4);
        Assert.Equal(0f, forward.Z, 4);
    }

    [Fact]
    public void ComputeUp_IdentityRotation_ReturnsPositiveY()
    {
        var up = CameraRigSystem.ComputeUp(Quaternion.Identity);

        Assert.Equal(Vector3.UnitY, up);
    }

    [Fact]
    public void ComputeUp_NinetyDegreeRollAroundForward_TiltsTowardAxis()
    {
        // Rolling 90 degrees around Z (the identity forward's opposite axis) should tip "up" onto X.
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

        var up = CameraRigSystem.ComputeUp(rotation);

        Assert.Equal(-1f, up.X, 4);
        Assert.Equal(0f, up.Y, 4);
        Assert.Equal(0f, up.Z, 4);
    }

    // ── Camera3D with none of the optional components ───────────────────────

    [Fact]
    public void Update_CameraWithNoOptionalComponents_LeavesCameraUnchanged()
    {
        var world = new World();
        var cam = world.CreateEntity();
        var original = new Camera3D(
            new Vector3(1f, 2f, 3f),
            new Vector3(0f, 0f, 0f),
            Vector3.UnitY,
            MathF.PI / 4f,
            0.1f,
            100f
        );
        world.AddComponent(cam, original);

        new CameraRigSystem(world).Update(1f / 60f);

        Assert.Equal(original, world.GetComponent<Camera3D>(cam));
    }

    // ── Transform3D bridge ────────────────────────────────────────────────

    [Fact]
    public void Update_Transform3DPresent_SyncsPositionTargetUpFromIt()
    {
        var world = new World();
        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(
            cam,
            new Transform3D(new Vector3(2f, 3f, 4f), Quaternion.Identity, Vector3.One)
        );

        new CameraRigSystem(world).Update(1f / 60f);

        var camera = world.GetComponent<Camera3D>(cam);
        Assert.Equal(new Vector3(2f, 3f, 4f), camera.Position);
        Assert.Equal(new Vector3(2f, 3f, 3f), camera.Target); // Position + (0,0,-1)
        Assert.Equal(Vector3.UnitY, camera.Up);
    }

    [Fact]
    public void Update_ParentedCamera_RidesMovingParentThroughHierarchy()
    {
        var world = new World();

        var parent = world.CreateEntity();
        world.AddComponent(parent, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));

        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(cam, new Parent(parent));
        world.AddComponent(
            cam,
            new LocalTransform3D(new Vector3(0f, 1f, 2f), Quaternion.Identity, Vector3.One)
        );

        var hierarchy = new TransformHierarchySystem(world);
        var rig = new CameraRigSystem(world);

        // Move the parent — e.g. a lift the camera rides inside.
        world.AddComponent(
            parent,
            new Transform3D(new Vector3(5f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );

        hierarchy.Update(0f);
        rig.Update(0f);

        var camera = world.GetComponent<Camera3D>(cam);
        AssertClose(new Vector3(5f, 1f, 2f), camera.Position);
        AssertClose(new Vector3(5f, 1f, 1f), camera.Target); // Position + (0,0,-1)
    }

    // ── LookAtTarget ─────────────────────────────────────────────────────

    [Fact]
    public void Update_LookAtTarget_ZeroSmoothing_SnapsTargetToEntityPosition()
    {
        var world = new World();
        var target = world.CreateEntity();
        world.AddComponent(
            target,
            new Transform3D(new Vector3(10f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );

        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(Vector3.Zero, Vector3.Zero, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(cam, new LookAtTarget(target, smoothing: 0f));

        new CameraRigSystem(world).Update(1f / 60f);

        Assert.Equal(new Vector3(10f, 0f, 0f), world.GetComponent<Camera3D>(cam).Target);
    }

    [Fact]
    public void Update_LookAtTarget_PositiveSmoothing_MovesTargetPartway()
    {
        var world = new World();
        var target = world.CreateEntity();
        world.AddComponent(
            target,
            new Transform3D(new Vector3(10f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );

        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(Vector3.Zero, Vector3.Zero, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(cam, new LookAtTarget(target, smoothing: 5f));

        new CameraRigSystem(world).Update(0.1f);

        var newTarget = world.GetComponent<Camera3D>(cam).Target;
        Assert.True(newTarget.X > 0f);
        Assert.True(newTarget.X < 10f);
    }

    [Fact]
    public void Update_LookAtTarget_TargetEntityDestroyed_HoldsLastTargetInsteadOfThrowing()
    {
        var world = new World();
        var target = world.CreateEntity();
        world.AddComponent(
            target,
            new Transform3D(new Vector3(10f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );

        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(
                Vector3.Zero,
                new Vector3(1f, 2f, 3f),
                Vector3.UnitY,
                MathF.PI / 4f,
                0.1f,
                100f
            )
        );
        world.AddComponent(cam, new LookAtTarget(target, smoothing: 0f));

        world.DestroyEntity(target);

        var exception = Record.Exception(() => new CameraRigSystem(world).Update(1f / 60f));

        Assert.Null(exception);
        // No Transform3D on the target entity anymore -> Target holds whatever it was.
        Assert.Equal(new Vector3(1f, 2f, 3f), world.GetComponent<Camera3D>(cam).Target);
    }

    // ── CameraShake ──────────────────────────────────────────────────────

    [Fact]
    public void Update_CameraShake_DecaysToZero_PositionAndTargetReturnToRest()
    {
        var world = new World();
        var cam = world.CreateEntity();
        var restPosition = new Vector3(1f, 2f, 3f);
        var restTarget = new Vector3(1f, 2f, 2f);
        world.AddComponent(
            cam,
            new Camera3D(restPosition, restTarget, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );

        var shake = new CameraShake(decay: 2f, maxOffset: 0.5f, seed: 11).AddTrauma(1f);
        world.AddComponent(cam, shake);

        var rig = new CameraRigSystem(world);

        // decay=2/s, dt=0.1s -> trauma reaches exactly 0 after 5 steps. Run 10 to also confirm it
        // stays settled afterwards (no residual drift once fully decayed).
        for (var i = 0; i < 10; i++)
            rig.Update(0.1f);

        var camera = world.GetComponent<Camera3D>(cam);
        Assert.Equal(restPosition.X, camera.Position.X, 4);
        Assert.Equal(restPosition.Y, camera.Position.Y, 4);
        Assert.Equal(restPosition.Z, camera.Position.Z, 4);
        Assert.Equal(restTarget.X, camera.Target.X, 4);
        Assert.Equal(restTarget.Y, camera.Target.Y, 4);
        Assert.Equal(restTarget.Z, camera.Target.Z, 4);
        Assert.Equal(0f, world.GetComponent<CameraShake>(cam).Trauma);
    }

    [Fact]
    public void Update_CameraShake_WhileActive_OffsetsPositionAwayFromRest()
    {
        var world = new World();
        var cam = world.CreateEntity();
        var restPosition = new Vector3(0f, 0f, 0f);
        world.AddComponent(
            cam,
            new Camera3D(restPosition, -Vector3.UnitZ, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(cam, new CameraShake(decay: 0f, maxOffset: 1f, seed: 5).AddTrauma(1f));

        new CameraRigSystem(world).Update(0.5f);

        var camera = world.GetComponent<Camera3D>(cam);
        Assert.NotEqual(restPosition, camera.Position);
    }

    [Fact]
    public void Update_CameraShake_CombinedWithRelativeMovement_DoesNotPermanentlyDriftPosition()
    {
        // Simulates FreeFlyCameraSystem-style relative movement (Position += displacement) running
        // before CameraRigSystem each frame, with a shake in progress. Once trauma fully decays,
        // the camera must be exactly where pure displacement accumulation would put it — proving
        // shake never permanently bakes into position.
        var world = new World();
        var cam = world.CreateEntity();
        world.AddComponent(
            cam,
            new Camera3D(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, MathF.PI / 4f, 0.1f, 100f)
        );
        world.AddComponent(
            cam,
            new CameraShake(decay: 2f, maxOffset: 0.5f, seed: 17).AddTrauma(1f)
        );

        var rig = new CameraRigSystem(world);
        var displacement = new Vector3(1f, 0f, 0f);
        const int frames = 10; // decay=2/s, dt=0.1s -> trauma settles to 0 by frame 5

        for (var i = 0; i < frames; i++)
        {
            var camera = world.GetComponent<Camera3D>(cam);
            camera.Position += displacement;
            camera.Target += displacement;
            world.AddComponent(cam, camera);

            rig.Update(0.1f);
        }

        var final = world.GetComponent<Camera3D>(cam);
        var expectedPosition = displacement * frames;
        Assert.Equal(expectedPosition.X, final.Position.X, 4);
        Assert.Equal(expectedPosition.Y, final.Position.Y, 4);
        Assert.Equal(expectedPosition.Z, final.Position.Z, 4);
    }

    [Fact]
    public void CameraShake_AddTrauma_ClampsToOne()
    {
        var shake = new CameraShake().AddTrauma(1.5f);

        Assert.Equal(1f, shake.Trauma);
    }

    [Fact]
    public void CameraShake_AddTrauma_AccumulatesOnTopOfExistingTrauma()
    {
        var shake = new CameraShake().AddTrauma(0.3f).AddTrauma(0.2f);

        Assert.Equal(0.5f, shake.Trauma, 4);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }
}

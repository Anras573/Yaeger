using System.Numerics;
using System.Runtime.InteropServices;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Renders every entity's <see cref="ParticleEmitter3D"/> pool (simulated by
/// <see cref="ParticleSystem3D"/>) as camera-facing billboards via <see cref="Renderer3D.DrawParticles"/>
/// — one <c>glDrawArraysInstanced</c> call per emitter. Native-only (takes a <see cref="Window"/>
/// for the camera, like <see cref="MeshRenderSystem"/>), so this lives outside <c>Yaeger.Core</c>
/// even though <see cref="ParticleSystem3D"/> itself doesn't.
///
/// Call <see cref="Render"/> from the render callback, after <see cref="MeshRenderSystem.Render"/>
/// (particles draw in their own pass, on top of the opaque/transparent mesh passes).
/// </summary>
/// <param name="sceneDepthSource">
/// Optional source of the opaque scene's depth, for soft particles (see
/// <see cref="ParticleEmitter3D.SoftFade"/>) — a <see cref="RenderTarget"/> constructed with a depth
/// attachment that the scene's opaque pass has already rendered into this frame (e.g. a
/// <see cref="PostProcessStack"/>'s own scene target). <c>null</c> (the default) disables soft
/// particles entirely: every emitter fades exactly as it did before this feature existed, regardless
/// of its own <c>SoftFade</c> value.
/// </param>
public class ParticleRenderSystem3D(
    Renderer3D renderer,
    TextureManager textureManager,
    World world,
    Window window,
    ParticleSystem3D particleSystem3D,
    RenderTarget? sceneDepthSource = null
)
{
    // Reused across frames so sorting the emitter draw order doesn't allocate — mirrors
    // MeshRenderSystem's _transparentDraws/_pointLights scratch buffers.
    private readonly List<(Entity Entity, Vector3 Position)> _transparentEmitters = [];
    private readonly List<(Entity Entity, Vector3 Position)> _additiveEmitters = [];
    private readonly List<ParticleInstanceData> _instanceScratch = [];

    public void Render()
    {
        var (view, viewProj, near, far) = GetCameraMatrices();
        var (cameraRight, cameraUp) = BillboardMath.ExtractCameraAxes(view);

        if (sceneDepthSource != null)
        {
            renderer.SetSceneDepth(
                sceneDepthSource.DepthTexture,
                near,
                far,
                sceneDepthSource.Width,
                sceneDepthSource.Height
            );
        }
        else
        {
            // Keep the opt-in robust the same way MeshRenderSystem's shadow/IBL branches do: clear
            // any stale depth-texture state so this scene doesn't sample a leftover/deleted texture.
            renderer.DisableSceneDepth();
        }

        _transparentEmitters.Clear();
        _additiveEmitters.Clear();

        var transformStore = world.GetStore<Transform3D>();
        foreach (var (entity, emitter) in world.GetStore<ParticleEmitter3D>().All())
        {
            if (!particleSystem3D.TryGetPool(entity, out var pool) || pool.AliveCount == 0)
                continue;
            if (!transformStore.TryGet(entity, out var transform))
                continue;

            // Additive is the only blend mode drawn without inter-emitter sorting (see
            // ParticleEmitter3D.BlendMode); Opaque/Cutout — meaningless for a billboard — fall back
            // to the sorted Transparent group like every other non-Additive value.
            var group =
                emitter.BlendMode == MaterialBlendMode.Additive
                    ? _additiveEmitters
                    : _transparentEmitters;
            group.Add((entity, transform.Position));
        }

        TransparencySorter.SortBackToFront(_transparentEmitters, view, e => e.Position);

        renderer.BeginTransparentPass();
        foreach (var (entity, _) in _transparentEmitters)
            DrawEmitter(entity, cameraRight, cameraUp, viewProj, MaterialBlendMode.Transparent);
        foreach (var (entity, _) in _additiveEmitters)
            DrawEmitter(entity, cameraRight, cameraUp, viewProj, MaterialBlendMode.Additive);
        renderer.EndTransparentPass();
    }

    private void DrawEmitter(
        Entity entity,
        Vector3 cameraRight,
        Vector3 cameraUp,
        Matrix4x4 viewProj,
        MaterialBlendMode blendMode
    )
    {
        if (!world.TryGetComponent<ParticleEmitter3D>(entity, out var emitter))
            return;
        if (!particleSystem3D.TryGetPool(entity, out var pool))
            return;

        var startColor = emitter.StartColor.ToVector4();
        var endColor = emitter.EndColor.ToVector4();
        var velocityStretch = MathF.Max(emitter.VelocityStretch, 0f);
        var totalFrames = Math.Max(emitter.FrameColumns, 1) * Math.Max(emitter.FrameRows, 1);

        _instanceScratch.Clear();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            ref readonly var particle = ref pool[i];
            var t = particle.NormalizedAge;
            var baseSize = MathF.Max(
                (emitter.StartSize + (emitter.EndSize - emitter.StartSize) * t)
                    * particle.SizeMultiplier,
                0f
            );
            var color = Vector4.Lerp(startColor, endColor, t);

            var (projectedSpeed, velocityRotation) = BillboardMath.ProjectVelocity(
                particle.Velocity,
                cameraRight,
                cameraUp
            );
            // A particle with no meaningful screen-space velocity falls back to the rotation it
            // spawned with (see ParticleEmitter3D.RandomInitialRotation) instead of always 0;
            // once it does have projected velocity, that direction owns the rotation, same as
            // before this feature existed.
            var rotation = projectedSpeed > 0f ? velocityRotation : particle.InitialRotation;
            var alongVelocity = baseSize + projectedSpeed * velocityStretch;

            var frameIndex = BillboardMath.ComputeFrameIndex(
                particle.StartFrame,
                particle.Age,
                emitter.FrameRate,
                totalFrames
            );
            var (uvMin, uvMax) = BillboardMath.GetFrameUv(
                emitter.FrameColumns,
                emitter.FrameRows,
                frameIndex
            );

            _instanceScratch.Add(
                new ParticleInstanceData(
                    particle.Position,
                    new Vector2(alongVelocity, baseSize),
                    rotation,
                    color,
                    new Vector4(uvMin.X, uvMin.Y, uvMax.X, uvMax.Y)
                )
            );
        }

        renderer.DrawParticles(
            CollectionsMarshal.AsSpan(_instanceScratch),
            cameraRight,
            cameraUp,
            viewProj,
            emitter.TexturePath,
            textureManager,
            blendMode,
            emitter.SoftFade
        );
    }

    private (Matrix4x4 View, Matrix4x4 ViewProj, float Near, float Far) GetCameraMatrices()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            var view = camera.ViewMatrix;
            return (view, view * camera.ProjectionMatrix(aspectRatio), camera.Near, camera.Far);
        }

        return (Matrix4x4.Identity, Matrix4x4.Identity, 0.1f, 1000f);
    }
}

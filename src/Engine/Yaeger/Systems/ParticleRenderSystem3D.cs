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
public class ParticleRenderSystem3D(
    Renderer3D renderer,
    TextureManager textureManager,
    World world,
    Window window,
    ParticleSystem3D particleSystem3D
)
{
    // Reused across frames so sorting the emitter draw order doesn't allocate — mirrors
    // MeshRenderSystem's _transparentDraws/_pointLights scratch buffers.
    private readonly List<(Entity Entity, Vector3 Position)> _transparentEmitters = [];
    private readonly List<(Entity Entity, Vector3 Position)> _additiveEmitters = [];
    private readonly List<ParticleInstanceData> _instanceScratch = [];

    public void Render()
    {
        var (view, viewProj) = GetCameraMatrices();
        var (cameraRight, cameraUp) = BillboardMath.ExtractCameraAxes(view);

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

        _instanceScratch.Clear();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            ref readonly var particle = ref pool[i];
            var t = particle.NormalizedAge;
            var baseSize = MathF.Max(
                emitter.StartSize + (emitter.EndSize - emitter.StartSize) * t,
                0f
            );
            var color = Vector4.Lerp(startColor, endColor, t);

            var (projectedSpeed, rotation) = BillboardMath.ProjectVelocity(
                particle.Velocity,
                cameraRight,
                cameraUp
            );
            var alongVelocity = baseSize + projectedSpeed * velocityStretch;

            _instanceScratch.Add(
                new ParticleInstanceData(
                    particle.Position,
                    new Vector2(alongVelocity, baseSize),
                    rotation,
                    color
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
            blendMode
        );
    }

    private (Matrix4x4 View, Matrix4x4 ViewProj) GetCameraMatrices()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            var view = camera.ViewMatrix;
            return (view, view * camera.ProjectionMatrix(aspectRatio));
        }

        return (Matrix4x4.Identity, Matrix4x4.Identity);
    }
}

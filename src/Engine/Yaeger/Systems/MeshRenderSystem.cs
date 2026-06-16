using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Queries ECS entities with <see cref="MeshHandle"/>, <see cref="Transform3D"/>, and
/// <see cref="Material3D"/> components and issues draw calls via <see cref="Renderer3D"/>.
/// Wire this to <see cref="Window.OnRender"/>, not <see cref="Window.OnUpdate"/>.
/// Pass a <see cref="SkyboxRenderer"/> and <see cref="CubemapRegistry"/> to render any
/// <see cref="Skybox"/> entity automatically. Pass a <see cref="ShadowMapRenderer"/> to render
/// directional-light shadows via an extra depth pre-pass.
/// </summary>
public class MeshRenderSystem(
    Renderer3D renderer,
    GpuMeshRegistry meshRegistry,
    TextureManager textureManager,
    World world,
    Window window,
    SkyboxRenderer? skyboxRenderer = null,
    CubemapRegistry? cubemapRegistry = null,
    ShadowMapRenderer? shadowMapRenderer = null
)
{
    // Reused across frames so collecting lights doesn't allocate per render call. Sized to the
    // renderer's hard caps (entities beyond the cap are simply ignored) and allocated lazily on
    // first use.
    private (Vector3 Position, PointLight Light)[]? _pointLights;
    private (Vector3 Position, SpotLight Light)[]? _spotLights;

    public void Render()
    {
        var (view, projection, cameraPos, sceneCenter, hasCamera) = GetCameraMatrices();
        var viewProj = view * projection;
        var light = GetDirectionalLight();
        CameraFrustum? frustum = hasCamera ? CameraFrustum.FromMatrix(viewProj) : null;
        var aabbStore = hasCamera ? world.GetStore<Aabb3D>() : null;

        // Collect first so the lazily-allocated buffers are populated before we slice them.
        var pointLightCount = CollectPointLights();
        var spotLightCount = CollectSpotLights();

        // Shadow pre-pass: render scene depth from the directional light's point of view. Runs
        // before BeginFrame3D so it owns the framebuffer/viewport state for its duration.
        if (shadowMapRenderer != null)
            RenderShadowPass(light, sceneCenter);

        renderer.BeginFrame3D();
        renderer.SetSceneLighting(light, cameraPos);
        renderer.SetPointLights(_pointLights!.AsSpan(0, pointLightCount));
        renderer.SetSpotLights(_spotLights!.AsSpan(0, spotLightCount));

        if (shadowMapRenderer != null)
        {
            var settings = shadowMapRenderer.Settings;
            renderer.SetShadowMap(
                shadowMapRenderer.LightSpaceMatrix,
                shadowMapRenderer.DepthTexture,
                settings.Bias,
                settings.EnablePcf
            );
        }
        else
        {
            // Keep the opt-in robust when a Renderer3D is shared with a shadow-casting system: clear
            // any stale shadow state so this scene doesn't sample a leftover/deleted depth texture.
            renderer.DisableShadows();
        }

        foreach (
            (
                Entity entity,
                MeshHandle handle,
                Transform3D transform,
                Material3D material
            ) in world.Query<MeshHandle, Transform3D, Material3D>()
        )
        {
            if (!meshRegistry.TryGet(handle, out var mesh))
                continue;

            var modelMatrix = transform.ModelMatrix;

            if (frustum.HasValue && aabbStore!.TryGet(entity, out var aabb))
            {
                if (!frustum.Value.Intersects(aabb, modelMatrix))
                    continue;
            }

            renderer.Draw(mesh, modelMatrix, viewProj, material, textureManager);
        }

        if (skyboxRenderer != null && cubemapRegistry != null && hasCamera)
        {
            foreach (var (_, skybox) in world.GetStore<Skybox>().All())
            {
                if (cubemapRegistry.TryGet(skybox, out var cubemap))
                {
                    skyboxRenderer.Draw(cubemap, view, projection);
                    break;
                }
            }
        }

        renderer.EndFrame3D();
    }

    // Renders every shadow caster into the shadow map from the light's perspective. Casters are not
    // frustum-culled against the camera: geometry behind or beside the view can still cast into it.
    private void RenderShadowPass(DirectionalLight light, Vector3 sceneCenter)
    {
        // Caller guards on shadowMapRenderer != null; hoist to a non-null local so the whole method
        // reads off a single, analysis-friendly reference.
        var shadowMap = shadowMapRenderer!;
        shadowMap.BeginPass(light, sceneCenter);

        foreach (
            (Entity _, MeshHandle handle, Transform3D transform, Material3D _) in world.Query<
                MeshHandle,
                Transform3D,
                Material3D
            >()
        )
        {
            if (meshRegistry.TryGet(handle, out var mesh))
                shadowMap.Draw(mesh, transform.ModelMatrix);
        }

        var size = window.Size;
        shadowMap.EndPass((int)size.X, (int)size.Y);
    }

    private (
        Matrix4x4 View,
        Matrix4x4 Projection,
        Vector3 CameraPos,
        Vector3 SceneCenter,
        bool HasCamera
    ) GetCameraMatrices()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            return (
                camera.ViewMatrix,
                camera.ProjectionMatrix(aspectRatio),
                camera.Position,
                camera.Target,
                true
            );
        }

        return (Matrix4x4.Identity, Matrix4x4.Identity, Vector3.Zero, Vector3.Zero, false);
    }

    // Fills _pointLights with up to MaxPointLights entities carrying a PointLight + Transform3D
    // and returns the count written. Iterates the PointLight store directly (struct enumerator,
    // no allocation) and probes Transform3D via TryGet, mirroring how world.Query works internally
    // but without the per-frame iterator allocation.
    private int CollectPointLights()
    {
        _pointLights ??= new (Vector3, PointLight)[Renderer3D.MaxPointLights];
        var transforms = world.GetStore<Transform3D>();
        var count = 0;
        foreach (var (entity, pointLight) in world.GetStore<PointLight>())
        {
            if (count >= _pointLights.Length)
                break;
            if (transforms.TryGet(entity, out var transform))
                _pointLights[count++] = (transform.Position, pointLight);
        }
        return count;
    }

    // Fills _spotLights with up to MaxSpotLights entities carrying a SpotLight + Transform3D and
    // returns the count written. Allocation-free, like CollectPointLights.
    private int CollectSpotLights()
    {
        _spotLights ??= new (Vector3, SpotLight)[Renderer3D.MaxSpotLights];
        var transforms = world.GetStore<Transform3D>();
        var count = 0;
        foreach (var (entity, spotLight) in world.GetStore<SpotLight>())
        {
            if (count >= _spotLights.Length)
                break;
            if (transforms.TryGet(entity, out var transform))
                _spotLights[count++] = (transform.Position, spotLight);
        }
        return count;
    }

    private static readonly DirectionalLight DefaultLight = DirectionalLight.Default;

    private DirectionalLight GetDirectionalLight()
    {
        foreach (var (_, light) in world.GetStore<DirectionalLight>().All())
            return light;

        return DefaultLight;
    }
}

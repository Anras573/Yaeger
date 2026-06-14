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
/// <see cref="Skybox"/> entity automatically.
/// </summary>
public class MeshRenderSystem(
    Renderer3D renderer,
    GpuMeshRegistry meshRegistry,
    TextureManager textureManager,
    World world,
    Window window,
    SkyboxRenderer? skyboxRenderer = null,
    CubemapRegistry? cubemapRegistry = null
)
{
    // Reused each frame so collecting lights doesn't allocate per render call. Sized to the
    // renderer's hard caps; entities beyond the cap are simply ignored.
    private readonly (Vector3 Position, PointLight Light)[] _pointLights = new (
        Vector3,
        PointLight
    )[Renderer3D.MaxPointLights];
    private readonly (Vector3 Position, SpotLight Light)[] _spotLights = new (
        Vector3,
        SpotLight
    )[Renderer3D.MaxSpotLights];

    public void Render()
    {
        var (view, projection, cameraPos, hasCamera) = GetCameraMatrices();
        var viewProj = view * projection;
        var light = GetDirectionalLight();
        CameraFrustum? frustum = hasCamera ? CameraFrustum.FromMatrix(viewProj) : null;
        var aabbStore = hasCamera ? world.GetStore<Aabb3D>() : null;

        renderer.BeginFrame3D();
        renderer.SetSceneLighting(light, cameraPos);
        renderer.SetPointLights(_pointLights.AsSpan(0, CollectPointLights()));
        renderer.SetSpotLights(_spotLights.AsSpan(0, CollectSpotLights()));

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

    private (
        Matrix4x4 View,
        Matrix4x4 Projection,
        Vector3 CameraPos,
        bool HasCamera
    ) GetCameraMatrices()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            return (camera.ViewMatrix, camera.ProjectionMatrix(aspectRatio), camera.Position, true);
        }

        return (Matrix4x4.Identity, Matrix4x4.Identity, Vector3.Zero, false);
    }

    // Fills _pointLights with up to MaxPointLights entities carrying a PointLight + Transform3D
    // and returns the count written. Iterating PointLight first keeps the probe set small.
    private int CollectPointLights()
    {
        var count = 0;
        foreach (var (_, pointLight, transform) in world.Query<PointLight, Transform3D>())
        {
            if (count >= _pointLights.Length)
                break;
            _pointLights[count++] = (transform.Position, pointLight);
        }
        return count;
    }

    // Fills _spotLights with up to MaxSpotLights entities carrying a SpotLight + Transform3D and
    // returns the count written.
    private int CollectSpotLights()
    {
        var count = 0;
        foreach (var (_, spotLight, transform) in world.Query<SpotLight, Transform3D>())
        {
            if (count >= _spotLights.Length)
                break;
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

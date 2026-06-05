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
/// </summary>
public class MeshRenderSystem(
    Renderer3D renderer,
    GpuMeshRegistry meshRegistry,
    TextureManager textureManager,
    World world,
    Window window
)
{
    public void Render()
    {
        var (viewProj, cameraPos) = GetViewProjectionAndPosition();
        var light = GetDirectionalLight();
        var frustum = CameraFrustum.FromMatrix(viewProj);
        var aabbStore = world.GetStore<Aabb3D>();

        renderer.BeginFrame3D();
        renderer.SetSceneLighting(light, cameraPos);

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

            if (aabbStore.TryGet(entity, out var aabb))
            {
                if (!frustum.Intersects(aabb, transform.ModelMatrix))
                    continue;
            }

            renderer.Draw(mesh, transform.ModelMatrix, viewProj, material, textureManager);
        }

        renderer.EndFrame3D();
    }

    private (Matrix4x4 ViewProj, Vector3 CameraPos) GetViewProjectionAndPosition()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            return (camera.ViewProjection(aspectRatio), camera.Position);
        }

        return (Matrix4x4.Identity, Vector3.Zero);
    }

    private static readonly DirectionalLight DefaultLight = DirectionalLight.Default;

    private DirectionalLight GetDirectionalLight()
    {
        foreach (var (_, light) in world.GetStore<DirectionalLight>().All())
            return light;

        return DefaultLight;
    }
}

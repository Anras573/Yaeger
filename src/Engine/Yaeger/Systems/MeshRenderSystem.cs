using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Queries ECS entities with <see cref="MeshHandle"/>, <see cref="Transform3D"/>, and
/// <see cref="Material3D"/> components and issues draw calls via <see cref="Renderer3D"/>.
/// </summary>
public class MeshRenderSystem(
    Renderer3D renderer,
    GpuMeshRegistry meshRegistry,
    TextureManager textureManager,
    World world,
    Window window
) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        var viewProj = GetViewProjection();

        renderer.BeginFrame3D();

        foreach (
            (_, MeshHandle handle, Transform3D transform, Material3D material) in world.Query<
                MeshHandle,
                Transform3D,
                Material3D
            >()
        )
        {
            if (!meshRegistry.TryGet(handle, out var mesh))
                continue;

            renderer.Draw(mesh, transform.ModelMatrix, viewProj, material, textureManager);
        }

        renderer.EndFrame3D();
    }

    private Matrix4x4 GetViewProjection()
    {
        foreach (var (_, camera) in world.GetStore<Camera3D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0f ? size.X / size.Y : 1f;
            return camera.ViewProjection(aspectRatio);
        }

        return Matrix4x4.Identity;
    }
}

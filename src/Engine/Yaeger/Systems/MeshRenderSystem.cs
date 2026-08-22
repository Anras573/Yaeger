using System.Numerics;
using System.Runtime.InteropServices;
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
/// <see cref="Skybox"/> entity automatically, or a <see cref="ProceduralSkyRenderer"/> to render any
/// <see cref="ProceduralSky"/> entity the same way — the two sky kinds are independent and dispatched
/// by which component the scene carries. Pass a <see cref="ShadowMapRenderer"/> to render
/// directional-light shadows via an extra depth pre-pass. Pass an <see cref="EnvironmentMapRegistry"/>
/// to light PBR materials from the cubemap skybox (image-based lighting), or a
/// <see cref="ProceduralSkyIbl"/> to light them from a <see cref="ProceduralSky"/> instead — when
/// both would apply, the cubemap skybox's environment map wins (matching the draw-order precedence
/// between the two sky kinds below); scenes with neither, or without a baked/registered
/// <see cref="EnvironmentMap"/>, keep the flat ambient term.
/// </summary>
public class MeshRenderSystem(
    Renderer3D renderer,
    GpuMeshRegistry meshRegistry,
    TextureManager textureManager,
    World world,
    Window window,
    SkyboxRenderer? skyboxRenderer = null,
    CubemapRegistry? cubemapRegistry = null,
    ShadowMapRenderer? shadowMapRenderer = null,
    EnvironmentMapRegistry? environmentMaps = null,
    ProceduralSkyRenderer? proceduralSkyRenderer = null,
    ProceduralSkyIbl? proceduralSkyIbl = null
)
{
    /// <summary>
    /// Minimum entity count a (mesh, material) group needs before it's drawn via
    /// <see cref="Renderer3D.DrawInstanced"/> instead of one <see cref="Renderer3D.Draw(GpuMesh, Matrix4x4, Matrix4x4, Material3D, TextureManager)"/>
    /// call per entity. A small group isn't worth the extra instance-buffer upload, so it stays on
    /// the immediate path. Public and mutable so a caller (or a sample comparing the two paths) can
    /// tune or effectively disable instancing by setting it very high.
    /// </summary>
    public int InstancingThreshold { get; set; } = 4;

    // Reused across frames so collecting lights doesn't allocate per render call. Sized to the
    // renderer's hard caps (entities beyond the cap are simply ignored) and allocated lazily on
    // first use.
    private DirectionalLight[]? _directionalLights;
    private (Vector3 Position, PointLight Light)[]? _pointLights;
    private (Vector3 Position, SpotLight Light)[]? _spotLights;

    // Groups non-skinned entities by (MeshHandle, Material3D) for the main pass. Reused across
    // frames (see MeshInstanceBatcher's own remarks).
    private readonly MeshInstanceBatcher _mainBatcher = new();

    // Groups shadow casters by MeshHandle only: every entity is added with the same placeholder
    // Material3D key (default) since the shadow depth pass never reads material state, so this
    // collapses casters that share a mesh but differ in material into one instanced draw too.
    private readonly MeshInstanceBatcher _shadowBatcher = new();

    // A single Transparent- or Additive-blend-mode submission (see TransparencySorter.IsTransparent),
    // collected during the main query loop and drawn individually (never instanced/batched —
    // instancing would collapse several entities' relative depth into one draw, breaking
    // back-to-front ordering) after every opaque/cutout draw. bonePalette is null for non-skinned
    // entities.
    private readonly record struct TransparentDraw(
        MeshHandle Handle,
        Material3D Material,
        Matrix4x4 Model,
        Matrix4x4[]? BonePalette
    );

    // Reused across frames like _mainBatcher/_shadowBatcher above; cleared (not reallocated) each
    // frame so a scene with a stable transparent-entity count doesn't reallocate once warmed up.
    private readonly List<TransparentDraw> _transparentDraws = new();

    public void Render()
    {
        var (view, projection, cameraPos, sceneCenter, hasCamera) = GetCameraMatrices();
        var viewProj = view * projection;
        var directionalCount = CollectDirectionalLights();
        var directionalLights = _directionalLights!.AsSpan(0, directionalCount);
        // One shadow map, so one caster: the brightest light, since that's the one whose shadows
        // read. At dusk the sun and moon are both dim and the choice barely matters visually; what
        // matters is that it doesn't flicker between them, which picking by intensity avoids.
        var shadowLightIndex = BrightestLightIndex(directionalLights);
        var hasSkybox = TryGetFirstSkybox(out var skybox);
        var hasProceduralSky = TryGetFirstProceduralSky(out var proceduralSky);
        CameraFrustum? frustum = hasCamera ? CameraFrustum.FromMatrix(viewProj) : null;
        var aabbStore = hasCamera ? world.GetStore<Aabb3D>() : null;
        var paletteStore = world.GetStore<BonePalette>();

        // Collect first so the lazily-allocated buffers are populated before we slice them.
        var pointLightCount = CollectPointLights();
        var spotLightCount = CollectSpotLights();

        // Shadow pre-pass: render scene depth from the casting light's point of view. Runs before
        // BeginFrame3D so it owns the framebuffer/viewport state for its duration. A light at or
        // below the horizon has zero shadow strength, so the whole pass is skipped for it.
        var shadowLight =
            shadowLightIndex >= 0 ? directionalLights[shadowLightIndex] : DefaultLight;
        // Computed before the pass rather than read back from it, because it decides whether the
        // pass runs at all; the value uploaded below comes from the pass itself.
        var castsShadows =
            shadowMapRenderer != null
            && shadowLightIndex >= 0
            && ShadowMapRenderer.ComputeShadowStrength(shadowLight, shadowMapRenderer.Settings)
                > 0f;

        if (castsShadows)
            RenderShadowPass(shadowLight, sceneCenter, paletteStore);

        renderer.BeginFrame3D();
        renderer.SetSceneLighting(directionalLights, cameraPos);
        // Uploaded unconditionally (falling back to AmbientLight.Default) for the same reason
        // DisableShadows/DisableIBL are called below: a Renderer3D shared between scenes must not
        // keep lighting this one with the previous scene's ambient.
        renderer.SetAmbient(GetAmbientLight());
        renderer.SetPointLights(_pointLights!.AsSpan(0, pointLightCount));
        renderer.SetSpotLights(_spotLights!.AsSpan(0, spotLightCount));

        if (castsShadows)
        {
            var settings = shadowMapRenderer!.Settings;
            renderer.SetShadowMap(
                shadowMapRenderer.LightSpaceMatrix,
                shadowMapRenderer.DepthTexture,
                settings.Bias,
                settings.EnablePcf,
                shadowLightIndex,
                shadowMapRenderer.ShadowStrength
            );
        }
        else
        {
            // Keep the opt-in robust when a Renderer3D is shared with a shadow-casting system: clear
            // any stale shadow state so this scene doesn't sample a leftover/deleted depth texture.
            renderer.DisableShadows();
        }

        if (
            environmentMaps != null
            && hasSkybox
            && environmentMaps.TryGet(skybox, out var environmentMap)
        )
        {
            renderer.SetEnvironmentMap(environmentMap!);
        }
        else if (
            proceduralSkyIbl != null
            && hasProceduralSky
            && proceduralSkyIbl.TryGet(out var skyEnvironmentMap)
        )
        {
            renderer.SetEnvironmentMap(skyEnvironmentMap!);
        }
        else
        {
            // Keep the opt-in robust the same way the shadow branch above does: clear any stale
            // environment-map state so this scene doesn't sample a leftover/deleted texture.
            renderer.DisableIBL();
        }

        if (TryGetFirstFog(out var fog))
        {
            renderer.SetFog(fog);
        }
        else
        {
            // Same "clear stale state" reasoning as the shadow/IBL branches above: a Renderer3D
            // shared with a fog-enabled scene must not leak that fog into this one.
            renderer.DisableFog();
        }

        _mainBatcher.Clear();
        _transparentDraws.Clear();

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

            // Skinned meshes carry a per-frame bone palette (written by SkeletalAnimationSystem);
            // route them through the immediate skinning draw path — bone palettes are per-entity
            // state, so they can't be folded into the instanced path below. Static meshes are
            // grouped instead of drawn immediately, so a group sharing mesh + material can collapse
            // into a single instanced draw call once collection finishes.
            var hasPalette =
                paletteStore.TryGet(entity, out var palette) && palette.Matrices is { Length: > 0 };

            // Transparent materials always go through the separate back-to-front sorted pass below
            // (never batched/instanced — see TransparentDraw's remarks), whether skinned or not.
            if (TransparencySorter.IsTransparent(material))
            {
                _transparentDraws.Add(
                    new TransparentDraw(
                        handle,
                        material,
                        modelMatrix,
                        hasPalette ? palette.Matrices : null
                    )
                );
            }
            else if (hasPalette)
            {
                renderer.Draw(
                    mesh,
                    modelMatrix,
                    viewProj,
                    material,
                    textureManager,
                    palette.Matrices
                );
            }
            else
            {
                _mainBatcher.Add(handle, material, modelMatrix);
            }
        }

        foreach (var group in _mainBatcher.Groups)
        {
            if (!meshRegistry.TryGet(group.Handle, out var mesh))
                continue;

            if (group.Models.Count >= InstancingThreshold)
            {
                renderer.DrawInstanced(
                    mesh,
                    CollectionsMarshal.AsSpan(group.Models),
                    viewProj,
                    group.Material,
                    textureManager
                );
            }
            else
            {
                foreach (var modelMatrix in group.Models)
                    renderer.Draw(mesh, modelMatrix, viewProj, group.Material, textureManager);
            }
        }

        if (skyboxRenderer != null && cubemapRegistry != null && hasCamera && hasSkybox)
        {
            if (cubemapRegistry.TryGet(skybox, out var cubemap))
                skyboxRenderer.Draw(cubemap, view, projection);
        }
        else if (proceduralSkyRenderer != null && hasCamera && hasProceduralSky)
        {
            proceduralSkyRenderer.Draw(proceduralSky, view, projection);
        }

        // Transparent/additive pass: sorted back-to-front by view depth so overlapping blended
        // objects (Transparent and Additive share this one sorted pass — see TransparencySorter)
        // blend correctly regardless of camera angle (object-level ordering only). A scene with no
        // transparent/additive materials never enters this block, so it never touches the
        // renderer's blend/depth-mask state — opaque-only rendering is unaffected by this feature
        // existing.
        if (_transparentDraws.Count > 0)
        {
            TransparencySorter.SortBackToFront(_transparentDraws, view, d => d.Model.Translation);

            renderer.BeginTransparentPass();
            foreach (var draw in _transparentDraws)
            {
                if (!meshRegistry.TryGet(draw.Handle, out var mesh))
                    continue;

                if (draw.BonePalette != null)
                    renderer.Draw(
                        mesh,
                        draw.Model,
                        viewProj,
                        draw.Material,
                        textureManager,
                        draw.BonePalette
                    );
                else
                    renderer.Draw(mesh, draw.Model, viewProj, draw.Material, textureManager);
            }
            renderer.EndTransparentPass();
        }

        renderer.EndFrame3D();
    }

    // Returns the first Skybox entity found (enumeration order is the store's, same as every
    // other "first X in the world" lookup here — see GetAmbientLight/GetCameraMatrices).
    private bool TryGetFirstSkybox(out Skybox skybox)
    {
        foreach (var (_, sky) in world.GetStore<Skybox>().All())
        {
            skybox = sky;
            return true;
        }

        skybox = default;
        return false;
    }

    // Returns the first ProceduralSky entity found, mirroring TryGetFirstSkybox's "first X in the
    // world" convention.
    private bool TryGetFirstProceduralSky(out ProceduralSky sky)
    {
        foreach (var (_, proceduralSky) in world.GetStore<ProceduralSky>().All())
        {
            sky = proceduralSky;
            return true;
        }

        sky = default;
        return false;
    }

    // Returns the first FogSettings entity found, mirroring TryGetFirstSkybox/GetAmbientLight's
    // "first X in the world" convention. Fog is opt-in (no AmbientLight-style Default fallback): a
    // world with none disables fog entirely, so a scene that never attaches one is unaffected.
    private bool TryGetFirstFog(out FogSettings fog)
    {
        foreach (var (_, settings) in world.GetStore<FogSettings>().All())
        {
            fog = settings;
            return true;
        }

        fog = default;
        return false;
    }

    // Renders every shadow caster into the shadow map from the light's perspective. Casters are not
    // frustum-culled against the camera: geometry behind or beside the view can still cast into it.
    private void RenderShadowPass(
        DirectionalLight light,
        Vector3 sceneCenter,
        ComponentStorage<BonePalette> paletteStore
    )
    {
        // Caller guards on shadowMapRenderer != null; hoist to a non-null local so the whole method
        // reads off a single, analysis-friendly reference.
        var shadowMap = shadowMapRenderer!;

        // Auto-fit reframes the light on the casters themselves rather than on the camera's target,
        // which is what makes one set of settings hold across a full sun arc. Without it, nothing
        // here changes: the configured extent is used and sceneCenter stays the camera's target.
        if (shadowMap.Settings.AutoFit && TryComputeCasterBounds(out var bounds))
            shadowMap.BeginPass(light, bounds.Center, bounds.Radius);
        else
            shadowMap.BeginPass(light, sceneCenter);

        // Depth-only: material doesn't affect the shadow map, so every caster is added under the
        // same placeholder Material3D key — entities sharing a mesh collapse into one instanced
        // group here even if their real materials (used by the main pass below) differ. Transparent
        // and Additive materials don't cast shadows at all (v1 limitation — see docs/pbr.md);
        // cutout materials still cast full (non-masked) shadows.
        _shadowBatcher.Clear();

        foreach (
            (
                Entity entity,
                MeshHandle handle,
                Transform3D transform,
                Material3D material
            ) in world.Query<MeshHandle, Transform3D, Material3D>()
        )
        {
            if (TransparencySorter.IsTransparent(material))
                continue;

            if (!meshRegistry.TryGet(handle, out var mesh))
                continue;

            // Skinned casters take the immediate per-entity skinning path — same reasoning as the
            // main pass above: a bone palette is per-entity state, so it can't be folded into the
            // instanced group below. Non-skinned casters are unaffected, including the instanced path.
            var hasPalette =
                paletteStore.TryGet(entity, out var palette) && palette.Matrices is { Length: > 0 };

            if (hasPalette)
                shadowMap.Draw(mesh, transform.ModelMatrix, palette.Matrices);
            else
                _shadowBatcher.Add(handle, default, transform.ModelMatrix);
        }

        foreach (var group in _shadowBatcher.Groups)
        {
            if (!meshRegistry.TryGet(group.Handle, out var mesh))
                continue;

            if (group.Models.Count >= InstancingThreshold)
                shadowMap.DrawInstanced(mesh, CollectionsMarshal.AsSpan(group.Models));
            else
                foreach (var modelMatrix in group.Models)
                    shadowMap.Draw(mesh, modelMatrix);
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
    private static readonly AmbientLight DefaultAmbient = AmbientLight.Default;

    // Bounding sphere of every entity carrying an Aabb3D + Transform3D, in world space. Skinned
    // meshes deliberately carry no Aabb3D (a bind-pose box doesn't bound an animated mesh — see
    // issue #197), so they don't contribute: a scene of nothing but skinned casters reports no
    // bounds and the configured extent is used instead.
    private bool TryComputeCasterBounds(out (Vector3 Center, float Radius) bounds)
    {
        var transforms = world.GetStore<Transform3D>();
        var found = false;
        var total = default(Aabb3D);

        foreach (var (entity, aabb) in world.GetStore<Aabb3D>())
        {
            if (!transforms.TryGet(entity, out var transform))
                continue;

            var worldAabb = aabb.Transform(transform.ModelMatrix);
            total = found ? total.Union(worldAabb) : worldAabb;
            found = true;
        }

        if (!found)
        {
            bounds = default;
            return false;
        }

        bounds = total.BoundingSphere();
        return bounds.Radius > 0f;
    }

    // Fills _directionalLights with up to Renderer3D.MaxDirectionalLights entities carrying a
    // DirectionalLight and returns the count written. A world with none falls back to a single
    // default light, preserving the behaviour of every scene that never creates one.
    private int CollectDirectionalLights()
    {
        _directionalLights ??= new DirectionalLight[Renderer3D.MaxDirectionalLights];
        var count = 0;
        foreach (var (_, light) in world.GetStore<DirectionalLight>())
        {
            if (count >= _directionalLights.Length)
                break;
            _directionalLights[count++] = light;
        }

        if (count == 0)
            _directionalLights[count++] = DefaultLight;

        return count;
    }

    // Index of the brightest light in the span, or -1 when the span is empty or every light is dark.
    private static int BrightestLightIndex(ReadOnlySpan<DirectionalLight> lights)
    {
        var best = -1;
        var brightest = 0f;
        for (var i = 0; i < lights.Length; i++)
        {
            var intensity = lights[i].Intensity;
            if (float.IsFinite(intensity) && intensity > brightest)
            {
                brightest = intensity;
                best = i;
            }
        }

        return best;
    }

    private AmbientLight GetAmbientLight()
    {
        foreach (var (_, ambient) in world.GetStore<AmbientLight>().All())
            return ambient;

        return DefaultAmbient;
    }
}

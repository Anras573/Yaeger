using System.Numerics;
using CornellBox;
using Yaeger;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Inspector;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Cornell Box demo — classic CG test scene built entirely from procedural geometry.
// No external assets required.
// Controls: WASD move, Q/E up/down, right-mouse-drag look, F1 toggle editor overlay, ESC exit.

using var window = Window.Create();
var world = new World();
using var registry = new GpuMeshRegistry(window.Gl);
using var textures = new TextureManager(window.Gl);

// Room dimensions: 2 × 2 × 2, open at z = +1 (front, where the camera sits).
// All vertex sequences are CCW when viewed from the interior-facing normal direction
// so that back-face culling removes the exterior of each surface.

void AddSurface(string tag, MeshData meshData, Material3D material)
{
    var entity = world.CreateEntity(tag);
    world.AddComponent(entity, registry.Register(meshData));
    world.AddComponent(entity, Transform3D.Identity);
    world.AddComponent(entity, material);
    world.AddComponent(entity, meshData.ToAabb());
}

void AddBox(string tag, Vector3 position, Vector3 scale, float rotationY, Material3D material)
{
    var meshData = MeshFactory.CreateBox(tag);
    var entity = world.CreateEntity(tag);
    world.AddComponent(entity, registry.Register(meshData));
    world.AddComponent(
        entity,
        new Transform3D(position, Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotationY), scale)
    );
    world.AddComponent(entity, material);
    world.AddComponent(entity, meshData.ToAabb());
}

static Material3D Matte(Color diffuse) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color(
            (byte)(diffuse.R * 0.4f),
            (byte)(diffuse.G * 0.4f),
            (byte)(diffuse.B * 0.4f)
        ),
        Diffuse = diffuse,
        Specular = new Color(15, 15, 15),
        Shininess = 16f,
    };

static Material3D Emissive(Color color) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = color,
        Diffuse = color,
        Specular = Color.White,
        Shininess = 0f,
    };

// Additive: adds its colour to whatever is behind it (glBlendFunc(SrcAlpha, One)), so it can only
// brighten the frame — never darken it. Contrast this with Glass below over the same background.
static Material3D Glow(Color color) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = color,
        Diffuse = color,
        Specular = Color.Black,
        Shininess = 0f,
        Opacity = 0.6f,
        BlendMode = MaterialBlendMode.Additive,
    };

// Transparent: interpolates between its colour and whatever is behind it (glBlendFunc(SrcAlpha,
// OneMinusSrcAlpha)), so it tints/darkens rather than brightens.
static Material3D Glass(Color diffuse) =>
    new()
    {
        DiffuseTexturePath = string.Empty,
        Ambient = new Color((byte)(diffuse.R / 4), (byte)(diffuse.G / 4), (byte)(diffuse.B / 4)),
        Diffuse = diffuse,
        Specular = Color.White,
        Shininess = 64f,
        Opacity = 0.35f,
        BlendMode = MaterialBlendMode.Transparent,
    };

var white = Matte(new Color(220, 220, 220));
var red = Matte(new Color(160, 17, 13)); // Cornell left wall
var green = Matte(new Color(36, 115, 23)); // Cornell right wall
var boxGray = Matte(new Color(200, 200, 200));

// Floor  (y = 0, normal +Y)
AddSurface(
    "floor",
    MeshFactory.CreateQuad(
        "floor",
        new Vector3(-1f, 0f, 1f),
        new Vector3(1f, 0f, 1f),
        new Vector3(1f, 0f, -1f),
        new Vector3(-1f, 0f, -1f),
        Vector3.UnitY
    ),
    white
);

// Ceiling  (y = 2, normal -Y)
AddSurface(
    "ceiling",
    MeshFactory.CreateQuad(
        "ceiling",
        new Vector3(-1f, 2f, -1f),
        new Vector3(1f, 2f, -1f),
        new Vector3(1f, 2f, 1f),
        new Vector3(-1f, 2f, 1f),
        -Vector3.UnitY
    ),
    white
);

// Back wall  (z = -1, normal +Z)
AddSurface(
    "back_wall",
    MeshFactory.CreateQuad(
        "back_wall",
        new Vector3(-1f, 0f, -1f),
        new Vector3(1f, 0f, -1f),
        new Vector3(1f, 2f, -1f),
        new Vector3(-1f, 2f, -1f),
        Vector3.UnitZ
    ),
    white
);

// Left wall  (x = -1, normal +X)
AddSurface(
    "left_wall",
    MeshFactory.CreateQuad(
        "left_wall",
        new Vector3(-1f, 0f, 1f),
        new Vector3(-1f, 0f, -1f),
        new Vector3(-1f, 2f, -1f),
        new Vector3(-1f, 2f, 1f),
        Vector3.UnitX
    ),
    red
);

// Right wall  (x = +1, normal -X)
AddSurface(
    "right_wall",
    MeshFactory.CreateQuad(
        "right_wall",
        new Vector3(1f, 0f, -1f),
        new Vector3(1f, 0f, 1f),
        new Vector3(1f, 2f, 1f),
        new Vector3(1f, 2f, -1f),
        -Vector3.UnitX
    ),
    green
);

// Ceiling light panel — sits just below the ceiling to avoid z-fighting.
// The emissive material keeps it visually bright regardless of incoming light.
AddSurface(
    "light_panel",
    MeshFactory.CreateQuad(
        "light_panel",
        new Vector3(-0.25f, 1.98f, -0.25f),
        new Vector3(0.25f, 1.98f, -0.25f),
        new Vector3(0.25f, 1.98f, 0.25f),
        new Vector3(-0.25f, 1.98f, 0.25f),
        -Vector3.UnitY
    ),
    Emissive(Color.White)
);

// Tall box — slightly rotated, left-of-centre
AddBox(
    "tall_box",
    position: new Vector3(-0.33f, 0.3f, -0.3f),
    scale: new Vector3(0.3f, 0.6f, 0.3f),
    rotationY: MathF.PI / 12f, // 15°
    material: boxGray
);

// Short box — slightly rotated, right-of-centre
AddBox(
    "short_box",
    position: new Vector3(0.33f, 0.15f, 0.05f),
    scale: new Vector3(0.3f, 0.3f, 0.3f),
    rotationY: -MathF.PI / 12f,
    material: boxGray
);

// Additive vs. transparent demo — two floating panels facing the camera, side by side over the
// same back-wall background, so the difference between blend modes is visible at a glance: the
// additive panel brightens the wall behind it, the transparent panel tints/darkens it.
AddSurface(
    "glow_panel",
    MeshFactory.CreateQuad(
        "glow_panel",
        new Vector3(-0.55f, 0.9f, -0.55f),
        new Vector3(-0.15f, 0.9f, -0.55f),
        new Vector3(-0.15f, 1.3f, -0.55f),
        new Vector3(-0.55f, 1.3f, -0.55f),
        Vector3.UnitZ
    ),
    Glow(new Color(0, 220, 220))
);

AddSurface(
    "glass_panel",
    MeshFactory.CreateQuad(
        "glass_panel",
        new Vector3(0.15f, 0.9f, -0.55f),
        new Vector3(0.55f, 0.9f, -0.55f),
        new Vector3(0.55f, 1.3f, -0.55f),
        new Vector3(0.15f, 1.3f, -0.55f),
        Vector3.UnitZ
    ),
    Glass(new Color(150, 200, 255))
);

// Camera — positioned just outside the open front face, looking into the box.
var cameraEntity = world.CreateEntity("camera");
world.AddComponent(
    cameraEntity,
    new Camera3D(
        Position: new Vector3(0f, 1f, 3.2f),
        Target: new Vector3(0f, 1f, 0f),
        Up: Vector3.UnitY,
        Fov: MathF.PI / 4f,
        Near: 0.1f,
        Far: 100f
    )
);

// Directional light pointing upward toward the ceiling panel.
// Direction follows the convention: from fragment toward light source.
var lightEntity = world.CreateEntity("light");
world.AddComponent(
    lightEntity,
    new DirectionalLight
    {
        // Angled slightly off-vertical so the boxes throw distinct shadows across the floor.
        Direction = Vector3.Normalize(new Vector3(0.25f, 1f, 0.2f)),
        Color = Color.White,
        Intensity = 1.2f,
    }
);

// Coloured point lights — demonstrate multiple light sources casting distinct pools of colour
// across the walls and boxes. Each is placed via a Transform3D; MeshRenderSystem queries them.
void AddPointLight(string tag, Vector3 position, Color color, float intensity, float range)
{
    var entity = world.CreateEntity(tag);
    world.AddComponent(entity, new Transform3D(position, Quaternion.Identity, Vector3.One));
    world.AddComponent(
        entity,
        new PointLight
        {
            Color = color,
            Intensity = intensity,
            Range = range,
        }
    );
}

AddPointLight("light_red", new Vector3(-0.6f, 1.3f, 0.4f), Color.Red, 2.5f, 2.5f);
AddPointLight("light_green", new Vector3(0.6f, 1.3f, 0.4f), Color.Green, 2.5f, 2.5f);
AddPointLight("light_blue", new Vector3(0f, 0.6f, -0.6f), Color.Blue, 2.5f, 2.5f);

const string particleTexture = "Assets/particle.png";

// Embers — additive, round billboards drifting up from the short box. VelocityStretch stays 0
// (the default) so these stay round puffs of glow rather than streaks.
var embersEntity = world.CreateEntity("embers");
world.AddComponent(
    embersEntity,
    new Transform3D(new Vector3(0.33f, 0.3f, 0.05f), Quaternion.Identity, Vector3.One)
);
world.AddComponent(
    embersEntity,
    new ParticleEmitter3D(particleTexture)
    {
        MaxParticles = 128,
        EmitRate = 20f,
        ParticleLifetime = 1.5f,
        EmitDirection = Vector3.UnitY,
        SpreadAngle = MathF.PI / 6f,
        InitialSpeed = 0.25f,
        StartColor = new Color(255, 160, 40, 220),
        EndColor = new Color(255, 60, 0, 0),
        StartSize = 0.05f,
        EndSize = 0.015f,
        BlendMode = MaterialBlendMode.Additive,
    }
);

// Sparks — additive too, but VelocityStretch elongates each billboard into a streak aligned with
// its direction of travel, contrasting with the round embers above.
var sparksEntity = world.CreateEntity("sparks");
world.AddComponent(
    sparksEntity,
    new Transform3D(new Vector3(-0.33f, 0.62f, -0.3f), Quaternion.Identity, Vector3.One)
);
world.AddComponent(
    sparksEntity,
    new ParticleEmitter3D(particleTexture)
    {
        MaxParticles = 64,
        EmitRate = 15f,
        ParticleLifetime = 0.6f,
        EmitDirection = Vector3.UnitX,
        SpreadAngle = MathF.PI,
        InitialSpeed = 1.2f,
        StartColor = new Color(255, 240, 180, 255),
        EndColor = new Color(255, 120, 0, 0),
        StartSize = 0.02f,
        EndSize = 0.01f,
        BlendMode = MaterialBlendMode.Additive,
        VelocityStretch = 0.05f,
    }
);

var particleSystem3D = new ParticleSystem3D(world);

using var renderer3D = new Renderer3D(window.Gl);

// Directional-light shadow mapping. The orthographic frustum is sized to frame the 2×2×2 room
// (centred on the camera target), and PCF softens the cast-shadow edges.
using var shadowMapRenderer = new ShadowMapRenderer(
    window.Gl,
    new ShadowSettings
    {
        MapResolution = 2048,
        OrthographicSize = 2.5f,
        NearPlane = 0.1f,
        FarPlane = 12f,
        Bias = 0.004f,
        EnablePcf = true,
    }
);

var meshRenderSystem = new MeshRenderSystem(
    renderer3D,
    registry,
    textures,
    world,
    window,
    shadowMapRenderer: shadowMapRenderer
);
var particleRenderSystem3D = new ParticleRenderSystem3D(
    renderer3D,
    textures,
    world,
    window,
    particleSystem3D
);
var freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 3f);

// Editor overlay — lists every entity and lets you live-edit the attached 3D components
// (transforms, materials, lights, the camera). Toggle with F1.
using var inspector = new ImGuiInspector(window, world);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);

window.OnUpdate += deltaTime =>
{
    freeFlySystem.Update((float)deltaTime);
    particleSystem3D.Update((float)deltaTime);
};
window.OnRender += delta =>
{
    meshRenderSystem.Render();
    // After the mesh passes (opaque + its own sorted transparent pass), in its own pass: one
    // glDrawArraysInstanced call per emitter (Renderer3D.DrawCallCount) regardless of how many
    // particles are alive in it — orbit the camera (right-mouse-drag) to see the billboards stay
    // camera-facing from any angle.
    particleRenderSystem3D.Render();
    inspector.Render(delta);
};

window.Run();

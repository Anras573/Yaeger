using System.Numerics;
using CornellBox;
using Yaeger;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Cornell Box demo — classic CG test scene built entirely from procedural geometry.
// No external assets required.
// Controls: WASD move, Q/E up/down, right-mouse-drag look, ESC exit.

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
        Direction = Vector3.Normalize(new Vector3(0f, 1f, 0.15f)),
        Color = Color.White,
        Intensity = 0.5f,
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

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(renderer3D, registry, textures, world, window);
var freeFlySystem = new FreeFlySystem(world, cameraEntity);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnUpdate += deltaTime => freeFlySystem.Update((float)deltaTime);
window.OnRender += _ => meshRenderSystem.Render();

window.Run();

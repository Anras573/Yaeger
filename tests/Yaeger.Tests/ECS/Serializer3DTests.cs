using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

/// <summary>
/// Round-trip and deserialization tests for the 3D component serializers
/// (<see cref="Transform3DSerializer"/>, <see cref="Camera3DSerializer"/>,
/// <see cref="Material3DSerializer"/>, <see cref="DirectionalLightSerializer"/>,
/// <see cref="PointLightSerializer"/>, <see cref="SpotLightSerializer"/>).
/// </summary>
public class Serializer3DTests
{
    // ── Transform3D ──────────────────────────────────────────────────────────

    [Fact]
    public void Transform3D_ShouldRoundTrip()
    {
        var rotation = Quaternion.CreateFromYawPitchRoll(0.3f, 0.5f, 0.7f);
        var original = new Transform3D(new Vector3(1f, 2f, 3f), rotation, new Vector3(4f, 5f, 6f));

        var reloaded = RoundTrip(original, "node");

        Assert.Equal(original.Position, reloaded.Position);
        Assert.Equal(original.Rotation.X, reloaded.Rotation.X, precision: 5);
        Assert.Equal(original.Rotation.Y, reloaded.Rotation.Y, precision: 5);
        Assert.Equal(original.Rotation.Z, reloaded.Rotation.Z, precision: 5);
        Assert.Equal(original.Rotation.W, reloaded.Rotation.W, precision: 5);
        Assert.Equal(original.Scale, reloaded.Scale);
    }

    [Fact]
    public void Transform3D_MissingProperties_ShouldDefaultToIdentity()
    {
        var component = Deserialize<Transform3D>("""{ "type": "Transform3D" }""");

        Assert.Equal(Transform3D.Identity, component);
    }

    [Fact]
    public void Transform3D_AcceptsObjectFormVectors()
    {
        var component = Deserialize<Transform3D>(
            """{ "type": "Transform3D", "position": { "x": 1, "y": 2, "z": 3 } }"""
        );

        Assert.Equal(new Vector3(1f, 2f, 3f), component.Position);
    }

    // ── Camera3D ─────────────────────────────────────────────────────────────

    [Fact]
    public void Camera3D_ShouldRoundTrip()
    {
        var original = new Camera3D(
            new Vector3(1f, 2f, 3f),
            new Vector3(4f, 5f, 6f),
            Vector3.UnitY,
            1.2f,
            0.5f,
            500f
        );

        var reloaded = RoundTrip(original, "cam");

        Assert.Equal(original, reloaded);
    }

    [Fact]
    public void Camera3D_MissingProperties_ShouldDefaultToCameraDefault()
    {
        var component = Deserialize<Camera3D>("""{ "type": "Camera3D" }""");

        Assert.Equal(Camera3D.Default, component);
    }

    // ── Material3D ───────────────────────────────────────────────────────────

    [Fact]
    public void Material3D_BlinnPhong_ShouldRoundTrip()
    {
        var original = new Material3D
        {
            DiffuseTexturePath = "Assets/wood.png",
            NormalTexturePath = "Assets/wood_n.png",
            Ambient = new Color(10, 20, 30),
            Diffuse = new Color(200, 180, 150),
            Specular = Color.White,
            Shininess = 32f,
        };

        var reloaded = RoundTrip(original, "mat");

        Assert.False(reloaded.UsePbr);
        Assert.Equal("Assets/wood.png", reloaded.DiffuseTexturePath);
        Assert.Equal("Assets/wood_n.png", reloaded.NormalTexturePath);
        AssertColorEqual(original.Ambient, reloaded.Ambient);
        AssertColorEqual(original.Diffuse, reloaded.Diffuse);
        AssertColorEqual(original.Specular, reloaded.Specular);
        Assert.Equal(32f, reloaded.Shininess, precision: 5);
    }

    [Fact]
    public void Material3D_Pbr_ShouldRoundTrip()
    {
        var original = new Material3D
        {
            UsePbr = true,
            MetallicRoughnessTexturePath = "Assets/mr.png",
            AoTexturePath = "Assets/ao.png",
            EmissiveTexturePath = "Assets/em.png",
            MetallicFactor = 0.25f,
            RoughnessFactor = 0.75f,
            EmissiveColor = new Color(5, 6, 7),
        };

        var reloaded = RoundTrip(original, "pbr");

        Assert.True(reloaded.UsePbr);
        Assert.Equal("Assets/mr.png", reloaded.MetallicRoughnessTexturePath);
        Assert.Equal("Assets/ao.png", reloaded.AoTexturePath);
        Assert.Equal("Assets/em.png", reloaded.EmissiveTexturePath);
        Assert.Equal(0.25f, reloaded.MetallicFactor, precision: 5);
        Assert.Equal(0.75f, reloaded.RoughnessFactor, precision: 5);
        AssertColorEqual(original.EmissiveColor, reloaded.EmissiveColor);
    }

    [Fact]
    public void Material3D_MissingProperties_ShouldUseConstructorDefaults()
    {
        var component = Deserialize<Material3D>("""{ "type": "Material3D" }""");
        var defaults = new Material3D();

        Assert.Equal(defaults.UsePbr, component.UsePbr);
        Assert.Equal(defaults.DiffuseTexturePath, component.DiffuseTexturePath);
        Assert.Equal(defaults.MetallicFactor, component.MetallicFactor, precision: 5);
        Assert.Equal(defaults.RoughnessFactor, component.RoughnessFactor, precision: 5);
    }

    [Fact]
    public void Material3D_UnsetTexturePaths_ShouldNotBeWritten()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Material3D { Diffuse = new Color(1, 2, 3) });

        var node = new Material3DSerializer().TrySerialize(world, entity);

        Assert.NotNull(node);
        var obj = node!.AsObject();
        // NormalTexturePath et al. are null by default and should be omitted entirely.
        Assert.False(obj.ContainsKey("normalTexturePath"));
        Assert.False(obj.ContainsKey("metallicRoughnessTexturePath"));
        // DiffuseTexturePath defaults to empty string and is likewise omitted.
        Assert.False(obj.ContainsKey("diffuseTexturePath"));
    }

    [Fact]
    public void Material3D_WhitespaceTexturePath_ShouldBeTreatedAsUnset()
    {
        // A whitespace-only path is neither written on save nor surfaced as a real value on load,
        // so it can never round-trip into a bogus asset reference.
        var component = Deserialize<Material3D>(
            """{ "type": "Material3D", "normalTexturePath": "   " }"""
        );

        Assert.Null(component.NormalTexturePath);
    }

    // ── DirectionalLight ─────────────────────────────────────────────────────

    [Fact]
    public void DirectionalLight_ShouldRoundTrip()
    {
        var original = new DirectionalLight
        {
            Direction = new Vector3(0.1f, -1f, 0.2f),
            Color = new Color(255, 200, 100),
            Intensity = 2.5f,
        };

        var reloaded = RoundTrip(original, "sun");

        Assert.Equal(original.Direction, reloaded.Direction);
        AssertColorEqual(original.Color, reloaded.Color);
        Assert.Equal(original.Intensity, reloaded.Intensity, precision: 5);
    }

    [Fact]
    public void DirectionalLight_MissingProperties_ShouldUseDefault()
    {
        var component = Deserialize<DirectionalLight>("""{ "type": "DirectionalLight" }""");
        var defaults = DirectionalLight.Default;

        Assert.Equal(defaults.Direction, component.Direction);
        AssertColorEqual(defaults.Color, component.Color);
        Assert.Equal(defaults.Intensity, component.Intensity, precision: 5);
    }

    // ── PointLight ───────────────────────────────────────────────────────────

    [Fact]
    public void PointLight_ShouldRoundTrip()
    {
        var original = new PointLight
        {
            Color = new Color(10, 20, 30, 40),
            Intensity = 3f,
            Range = 25f,
        };

        var reloaded = RoundTrip(original, "lamp");

        AssertColorEqual(original.Color, reloaded.Color);
        Assert.Equal(original.Intensity, reloaded.Intensity, precision: 5);
        Assert.Equal(original.Range, reloaded.Range, precision: 5);
    }

    // ── SpotLight ────────────────────────────────────────────────────────────

    [Fact]
    public void SpotLight_ShouldRoundTrip()
    {
        var original = new SpotLight
        {
            Color = new Color(50, 60, 70),
            Intensity = 4f,
            Direction = new Vector3(1f, 0f, -1f),
            InnerConeAngle = 0.2f,
            OuterConeAngle = 0.4f,
            Range = 15f,
        };

        var reloaded = RoundTrip(original, "flashlight");

        AssertColorEqual(original.Color, reloaded.Color);
        Assert.Equal(original.Intensity, reloaded.Intensity, precision: 5);
        Assert.Equal(original.Direction, reloaded.Direction);
        Assert.Equal(original.InnerConeAngle, reloaded.InnerConeAngle, precision: 5);
        Assert.Equal(original.OuterConeAngle, reloaded.OuterConeAngle, precision: 5);
        Assert.Equal(original.Range, reloaded.Range, precision: 5);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void Vector3_WrongElementCount_ThrowsPrefabLoadException()
    {
        Assert.Throws<PrefabLoadException>(() =>
            Deserialize<Transform3D>("""{ "type": "Transform3D", "position": [1, 2] }""")
        );
    }

    [Fact]
    public void Color_ChannelOutOfRange_ThrowsPrefabLoadException()
    {
        Assert.Throws<PrefabLoadException>(() =>
            Deserialize<PointLight>("""{ "type": "PointLight", "color": [0, 0, 300] }""")
        );
    }

    [Fact]
    public void Quaternion_WrongElementCount_ThrowsPrefabLoadException()
    {
        Assert.Throws<PrefabLoadException>(() =>
            Deserialize<Transform3D>("""{ "type": "Transform3D", "rotation": [0, 0, 1] }""")
        );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static T RoundTrip<T>(T component, string tag)
        where T : struct
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity(tag);
        world.AddComponent(entity, component);

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity(tag, out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<T>(reloadedEntity, out var result));
        return result;
    }

    private static T Deserialize<T>(string componentJson)
        where T : struct
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse($$"""{ "components": [ {{componentJson}} ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<T>(entity, out var component));
        return component;
    }

    private static void AssertColorEqual(Color expected, Color actual)
    {
        Assert.Equal(expected.R, actual.R);
        Assert.Equal(expected.G, actual.G);
        Assert.Equal(expected.B, actual.B);
        Assert.Equal(expected.A, actual.A);
    }
}

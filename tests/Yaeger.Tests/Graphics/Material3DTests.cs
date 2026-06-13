using Yaeger.Assets;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class Material3DTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(Material3D).IsValueType);
    }

    [Fact]
    public void Default_HasNullTexturePath()
    {
        var material = default(Material3D);

        Assert.Null(material.DiffuseTexturePath);
    }

    [Fact]
    public void Default_NormalTexturePathIsNull()
    {
        var material = default(Material3D);

        Assert.Null(material.NormalTexturePath);
    }

    [Fact]
    public void FromMtl_MapsAllFields()
    {
        var mtl = new MtlMaterial(
            Name: "TestMat",
            DiffuseTexturePath: "textures/wall.png",
            NormalTexturePath: null,
            AmbientColor: new Color(10, 20, 30),
            DiffuseColor: new Color(100, 150, 200),
            SpecularColor: new Color(255, 255, 255),
            Shininess: 32f
        );

        var material = Material3D.FromMtl(mtl);

        Assert.Equal("textures/wall.png", material.DiffuseTexturePath);
        Assert.Equal(new Color(10, 20, 30), material.Ambient);
        Assert.Equal(new Color(100, 150, 200), material.Diffuse);
        Assert.Equal(new Color(255, 255, 255), material.Specular);
        Assert.Equal(32f, material.Shininess);
    }

    [Fact]
    public void FromMtl_NormalTexturePath_MapsCorrectly()
    {
        var mtl = new MtlMaterial(
            Name: "BumpedMat",
            DiffuseTexturePath: "textures/wall.png",
            NormalTexturePath: "textures/wall_normal.png",
            AmbientColor: Color.Black,
            DiffuseColor: Color.White,
            SpecularColor: Color.Black,
            Shininess: 0f
        );

        var material = Material3D.FromMtl(mtl);

        Assert.Equal("textures/wall_normal.png", material.NormalTexturePath);
    }

    [Fact]
    public void FromMtl_NullTexturePath_BecomesEmptyString()
    {
        var mtl = new MtlMaterial(
            Name: "NoTex",
            DiffuseTexturePath: null,
            NormalTexturePath: null,
            AmbientColor: new Color(0, 0, 0),
            DiffuseColor: new Color(0, 0, 0),
            SpecularColor: new Color(0, 0, 0),
            Shininess: 0f
        );

        var material = Material3D.FromMtl(mtl);

        Assert.Equal(string.Empty, material.DiffuseTexturePath);
        Assert.Null(material.NormalTexturePath);
    }

    [Fact]
    public void FromModel_MapsAllFields()
    {
        var ambient = new Color(30, 30, 30, 255);
        var model = new ModelMaterial(
            Name: "stone",
            DiffuseTexturePath: "textures/stone.png",
            NormalTexturePath: null,
            DiffuseColor: new Color(200, 180, 160, 255),
            AmbientColor: ambient
        );

        var material = Material3D.FromModel(model);

        Assert.Equal("textures/stone.png", material.DiffuseTexturePath);
        Assert.Equal(ambient, material.Ambient);
        Assert.Equal(new Color(200, 180, 160, 255), material.Diffuse);
        Assert.Equal(Color.Black, material.Specular);
        Assert.Equal(0f, material.Shininess);
    }

    [Fact]
    public void FromModel_NormalTexturePath_MapsCorrectly()
    {
        var model = new ModelMaterial(
            Name: "stone",
            DiffuseTexturePath: "textures/stone.png",
            NormalTexturePath: "textures/stone_normal.png",
            DiffuseColor: Color.White,
            AmbientColor: Color.Black
        );

        var material = Material3D.FromModel(model);

        Assert.Equal("textures/stone_normal.png", material.NormalTexturePath);
    }

    [Fact]
    public void FromModel_NullTexturePath_BecomesEmptyString()
    {
        var model = new ModelMaterial(
            Name: "untextured",
            DiffuseTexturePath: null,
            NormalTexturePath: null,
            DiffuseColor: Color.White,
            AmbientColor: Color.Black
        );

        var material = Material3D.FromModel(model);

        Assert.Equal(string.Empty, material.DiffuseTexturePath);
        Assert.Null(material.NormalTexturePath);
    }

    [Fact]
    public void New_DefaultsMetallicAndRoughnessToGltfDefaults()
    {
        // Hand-authored PBR materials (`new Material3D { UsePbr = true, ... }`) should pick up
        // glTF's 1.0 factor defaults without the caller having to set them explicitly.
        var material = new Material3D { UsePbr = true };

        Assert.True(material.UsePbr);
        Assert.Equal(1f, material.MetallicFactor);
        Assert.Equal(1f, material.RoughnessFactor);
    }

    [Fact]
    public void Default_IsNotPbr()
    {
        var material = default(Material3D);

        Assert.False(material.UsePbr);
    }

    [Fact]
    public void FromMtl_DoesNotEnablePbr()
    {
        var mtl = new MtlMaterial(
            Name: "TestMat",
            DiffuseTexturePath: "textures/wall.png",
            NormalTexturePath: null,
            AmbientColor: Color.Black,
            DiffuseColor: Color.White,
            SpecularColor: Color.White,
            Shininess: 32f
        );

        var material = Material3D.FromMtl(mtl);

        Assert.False(material.UsePbr);
    }

    [Fact]
    public void FromModel_MapsPbrFields()
    {
        var emissive = new Color(10, 20, 30, 255);
        var model = new ModelMaterial(
            Name: "metal",
            DiffuseTexturePath: "textures/base.png",
            NormalTexturePath: "textures/normal.png",
            DiffuseColor: Color.White,
            AmbientColor: Color.Black,
            MetallicRoughnessTexturePath: "textures/mr.png",
            AoTexturePath: "textures/ao.png",
            EmissiveTexturePath: "textures/emissive.png",
            MetallicFactor: 0.8f,
            RoughnessFactor: 0.3f,
            EmissiveColor: emissive,
            UsePbr: true
        );

        var material = Material3D.FromModel(model);

        Assert.True(material.UsePbr);
        Assert.Equal("textures/mr.png", material.MetallicRoughnessTexturePath);
        Assert.Equal("textures/ao.png", material.AoTexturePath);
        Assert.Equal("textures/emissive.png", material.EmissiveTexturePath);
        Assert.Equal(0.8f, material.MetallicFactor);
        Assert.Equal(0.3f, material.RoughnessFactor);
        Assert.Equal(emissive, material.EmissiveColor);
    }

    [Fact]
    public void FromModel_NonPbr_LeavesPbrDisabled()
    {
        var model = new ModelMaterial(
            Name: "stone",
            DiffuseTexturePath: "textures/stone.png",
            NormalTexturePath: null,
            DiffuseColor: Color.White,
            AmbientColor: Color.Black
        );

        var material = Material3D.FromModel(model);

        Assert.False(material.UsePbr);
        Assert.Null(material.MetallicRoughnessTexturePath);
        Assert.Null(material.AoTexturePath);
        Assert.Null(material.EmissiveTexturePath);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new Material3D
        {
            DiffuseTexturePath = "tex.png",
            NormalTexturePath = null,
            Ambient = new Color(1, 2, 3),
            Diffuse = new Color(4, 5, 6),
            Specular = new Color(7, 8, 9),
            Shininess = 16f,
        };
        var b = new Material3D
        {
            DiffuseTexturePath = "tex.png",
            NormalTexturePath = null,
            Ambient = new Color(1, 2, 3),
            Diffuse = new Color(4, 5, 6),
            Specular = new Color(7, 8, 9),
            Shininess = 16f,
        };

        Assert.Equal(a, b);
    }
}

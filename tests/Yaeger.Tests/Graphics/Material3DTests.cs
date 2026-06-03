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
    public void Default_HasEmptyTexturePath()
    {
        var material = default(Material3D);

        Assert.Null(material.DiffuseTexturePath);
    }

    [Fact]
    public void FromMtl_MapsAllFields()
    {
        var mtl = new MtlMaterial(
            Name: "TestMat",
            DiffuseTexturePath: "textures/wall.png",
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
    public void FromMtl_NullTexturePath_BecomesEmptyString()
    {
        var mtl = new MtlMaterial(
            Name: "NoTex",
            DiffuseTexturePath: null,
            AmbientColor: new Color(0, 0, 0),
            DiffuseColor: new Color(0, 0, 0),
            SpecularColor: new Color(0, 0, 0),
            Shininess: 0f
        );

        var material = Material3D.FromMtl(mtl);

        Assert.Equal(string.Empty, material.DiffuseTexturePath);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new Material3D
        {
            DiffuseTexturePath = "tex.png",
            Ambient = new Color(1, 2, 3),
            Diffuse = new Color(4, 5, 6),
            Specular = new Color(7, 8, 9),
            Shininess = 16f,
        };
        var b = new Material3D
        {
            DiffuseTexturePath = "tex.png",
            Ambient = new Color(1, 2, 3),
            Diffuse = new Color(4, 5, 6),
            Specular = new Color(7, 8, 9),
            Shininess = 16f,
        };

        Assert.Equal(a, b);
    }
}

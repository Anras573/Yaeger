using Yaeger.Assets;
using Yaeger.Graphics;

namespace Yaeger.Tests.Assets;

public class MtlLoaderTests
{
    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_SingleMaterial_ShouldReturnCorrectTexturePathAndColors()
    {
        var mtl = """
            newmtl stone_wall
            Ka 0.2 0.2 0.2
            Kd 0.8 0.6 0.4
            Ks 1.0 1.0 1.0
            Ns 32.0
            map_Kd textures/stone.png
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            Assert.Single(materials);
            var mat = materials["stone_wall"];
            Assert.Equal("stone_wall", mat.Name);
            Assert.Equal("textures/stone.png", mat.DiffuseTexturePath);
            Assert.Equal((byte)(0.2f * 255f), mat.AmbientColor.R);
            Assert.Equal((byte)(0.8f * 255f), mat.DiffuseColor.R);
            Assert.Equal((byte)(0.6f * 255f), mat.DiffuseColor.G);
            Assert.Equal((byte)(0.4f * 255f), mat.DiffuseColor.B);
            Assert.Equal((byte)255, mat.SpecularColor.R);
            Assert.Equal(32.0f, mat.Shininess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MultipleMaterials_ShouldReturnAllMaterials()
    {
        var mtl = """
            newmtl mat_a
            Kd 1.0 0.0 0.0
            Ns 10.0

            newmtl mat_b
            Kd 0.0 1.0 0.0
            Ns 20.0
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            Assert.Equal(2, materials.Count);
            Assert.True(materials.ContainsKey("mat_a"));
            Assert.True(materials.ContainsKey("mat_b"));
            Assert.Equal(10.0f, materials["mat_a"].Shininess);
            Assert.Equal(20.0f, materials["mat_b"].Shininess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_NoTexture_ShouldHaveNullDiffuseTexturePath()
    {
        var mtl = """
            newmtl plain
            Kd 0.5 0.5 0.5
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            Assert.Null(materials["plain"].DiffuseTexturePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_CommentsAndBlankLines_ShouldBeIgnored()
    {
        var mtl = """
            # This is a comment

            newmtl test_mat
            # Another comment
            Ka 0.1 0.1 0.1
            Kd 0.5 0.5 0.5
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            Assert.Single(materials);
            Assert.Equal("test_mat", materials["test_mat"].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_EmptyNewmtlName_ShouldThrowFormatException()
    {
        var mtl = """
            newmtl
            Kd 1.0 0.0 0.0
            """;
        var path = WriteTempFile(mtl);

        try
        {
            Assert.Throws<FormatException>(() => MtlLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_FileNotFound_ShouldThrowFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => MtlLoader.Load("nonexistent.mtl"));
    }

    [Fact]
    public void Load_NullOrWhiteSpacePath_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MtlLoader.Load("   "));
    }

    [Fact]
    public void Load_InlineComments_ShouldBeStripped()
    {
        var mtl = """
            newmtl inline_test # material name comment
            Kd 1.0 0.0 0.0 # red diffuse
            Ns 10.0 # shininess
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            Assert.Single(materials);
            Assert.Equal("inline_test", materials["inline_test"].Name);
            Assert.Equal((byte)255, materials["inline_test"].DiffuseColor.R);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MalformedColor_ShouldThrowFormatException()
    {
        var mtl = """
            newmtl bad
            Kd 1.0 0.5
            """;
        var path = WriteTempFile(mtl);

        try
        {
            Assert.Throws<FormatException>(() => MtlLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DefaultColors_AmbientIsBlackDiffuseIsWhite()
    {
        var mtl = """
            newmtl defaults
            Ns 0.0
            """;
        var path = WriteTempFile(mtl);

        try
        {
            var materials = MtlLoader.Load(path);

            var mat = materials["defaults"];
            Assert.Equal(Color.Black, mat.AmbientColor);
            Assert.Equal(Color.White, mat.DiffuseColor);
            Assert.Equal(Color.Black, mat.SpecularColor);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

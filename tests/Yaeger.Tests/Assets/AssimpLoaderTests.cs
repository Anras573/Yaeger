using System.Numerics;
using Silk.NET.Assimp;
using Xunit;
using Yaeger.Assets;
using Yaeger.Graphics;
using File = System.IO.File;

namespace Yaeger.Tests.Assets;

public class AssimpLoaderTests
{
    private static bool IsAssimpAvailable()
    {
        try
        {
            // GetErrorStringS makes a native call, forcing library load.
            // Returns empty string on success rather than throwing.
            Assimp.GetApi().GetErrorStringS();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string WriteTempObj(string content, string extension = ".obj")
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        File.WriteAllText(path, content);
        return path;
    }

    [SkippableFact]
    public void LoadScene_SingleTriangleObj_ShouldReturnCorrectVertexCount()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            f 1/1/1 2/2/1 3/3/1
            """;
        var path = WriteTempObj(obj);
        try
        {
            var scene = AssimpLoader.LoadScene(path);

            Assert.Single(scene.Meshes);
            Assert.Equal(3, scene.Meshes[0].Mesh.Vertices.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void LoadScene_SingleTriangleObj_ShouldPopulateNormalsAndTangents()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // A triangle lying flat in XY with texture coords — Assimp will compute tangents
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            f 1/1/1 2/2/1 3/3/1
            """;
        var path = WriteTempObj(obj);
        try
        {
            var scene = AssimpLoader.LoadScene(path);

            var mesh = scene.Meshes[0].Mesh;
            foreach (var v in mesh.Vertices)
            {
                Assert.NotEqual(Vector3.Zero, v.Normal);
                Assert.NotEqual(Vector3.Zero, v.Tangent);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void LoadScene_MaterialWithDiffuseTexture_ShouldResolvePathRelativeToFile()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        var tempDir = Path.GetTempPath();
        var mtlFile = Path.Combine(tempDir, Guid.NewGuid() + ".mtl");
        var objFile = Path.Combine(tempDir, Guid.NewGuid() + ".obj");
        const string textureFileName = "diffuse.png";

        File.WriteAllText(
            mtlFile,
            $"""
            newmtl stone
            map_Kd {textureFileName}
            """
        );

        File.WriteAllText(
            objFile,
            $"""
            mtllib {Path.GetFileName(mtlFile)}
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            usemtl stone
            f 1/1/1 2/2/1 3/3/1
            """
        );

        try
        {
            var scene = AssimpLoader.LoadScene(objFile);

            Assert.Single(scene.Meshes);
            var mat = scene.Meshes[0].Material;
            Assert.NotNull(mat.DiffuseTexturePath);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(tempDir, textureFileName)),
                mat.DiffuseTexturePath
            );
        }
        finally
        {
            File.Delete(objFile);
            File.Delete(mtlFile);
        }
    }

    [SkippableFact]
    public void LoadScene_DefaultNodeTransform_ShouldBeIdentity()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // OBJ doesn't carry node transforms, so the Transform3D should decompose to identity.
        // We verify the field is populated correctly for this default case.
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            f 1/1/1 2/2/1 3/3/1
            """;
        var path = WriteTempObj(obj);
        try
        {
            var scene = AssimpLoader.LoadScene(path);

            Assert.Single(scene.Meshes);
            var transform = scene.Meshes[0].Transform;
            Assert.Equal(Vector3.Zero, transform.Position);
            Assert.Equal(Quaternion.Identity, transform.Rotation);
            Assert.Equal(Vector3.One, transform.Scale);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadScene_FileNotFound_ShouldThrowFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => AssimpLoader.LoadScene("nonexistent.gltf"));
    }

    [Fact]
    public void LoadScene_NullOrWhiteSpacePath_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AssimpLoader.LoadScene("   "));
    }
}

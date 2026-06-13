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
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            // Silk.NET wraps missing native library as FileNotFoundException.
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

    [SkippableFact]
    public void LoadScene_GltfPbrMaterial_ShouldMapMetallicRoughnessAndEmissive()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // Minimal glTF 2.0 triangle whose material uses pbrMetallicRoughness. The buffer
        // (base64) holds three VEC3 positions followed by three unsigned-short indices.
        const string gltf = """
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [ { "nodes": [ 0 ] } ],
              "nodes": [ { "mesh": 0 } ],
              "meshes": [
                {
                  "primitives": [
                    { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }
                  ]
                }
              ],
              "materials": [
                {
                  "pbrMetallicRoughness": {
                    "baseColorFactor": [ 1.0, 1.0, 1.0, 1.0 ],
                    "metallicFactor": 0.25,
                    "roughnessFactor": 0.75
                  },
                  "emissiveFactor": [ 0.5, 0.0, 0.0 ]
                }
              ],
              "buffers": [
                {
                  "byteLength": 42,
                  "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIA"
                }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
              ],
              "accessors": [
                {
                  "bufferView": 0,
                  "componentType": 5126,
                  "count": 3,
                  "type": "VEC3",
                  "min": [ 0.0, 0.0, 0.0 ],
                  "max": [ 1.0, 1.0, 0.0 ]
                },
                { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ]
            }
            """;
        var path = WriteTempObj(gltf, ".gltf");
        try
        {
            var scene = AssimpLoader.LoadScene(path);

            Assert.Single(scene.Meshes);
            var mat = scene.Meshes[0].Material;

            Assert.True(mat.UsePbr);
            Assert.Equal(0.25f, mat.MetallicFactor, 3);
            Assert.Equal(0.75f, mat.RoughnessFactor, 3);
            // emissiveFactor R=0.5 → ~127 after the 0-255 quantisation; G and B stay 0.
            Assert.InRange(mat.EmissiveColor.R, (byte)120, (byte)135);
            Assert.Equal(0, mat.EmissiveColor.G);
            Assert.Equal(0, mat.EmissiveColor.B);
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

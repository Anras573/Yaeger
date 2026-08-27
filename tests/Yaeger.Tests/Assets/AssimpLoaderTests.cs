using System.Linq;
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

    [SkippableFact]
    public void LoadScene_GltfTranslucentBaseColor_ShouldMapOpacityAndBlendMode()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // Same minimal glTF triangle as the PBR test above, but with a translucent
        // baseColorFactor alpha — the "glTF alphaMode: BLEND" case from issue #149's scope.
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
                    "baseColorFactor": [ 1.0, 1.0, 1.0, 0.4 ]
                  },
                  "alphaMode": "BLEND"
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
            var modelMaterial = scene.Meshes[0].Material;
            Assert.Equal(0.4f, modelMaterial.Opacity, 2);

            var material = Material3D.FromModel(modelMaterial);
            Assert.Equal(0.4f, material.Opacity, 2);
            Assert.Equal(MaterialBlendMode.Transparent, material.BlendMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void LoadScene_StaticObj_ShouldHaveNoSkeletonOrAnimations()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // OBJ has no bones or animations: the skeleton stays null and the animation list is empty,
        // so existing static-mesh consumers are unaffected.
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

            Assert.Null(scene.Skeleton);
            Assert.Empty(scene.Animations);

            // Static vertices carry zero skin weights (identity skin in the shader).
            foreach (var v in scene.Meshes[0].Mesh.Vertices)
                Assert.Equal(Vector4.Zero, v.BoneWeights);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void LoadScene_SkinnedMeshWithNonIdentityNodeTransform_ShouldBakeTransformIntoVertices()
    {
        Skip.IfNot(IsAssimpAvailable(), "Native Assimp library not available.");

        // A skinned mesh's bone offset matrices (inverse bind poses) are computed relative to the
        // mesh NODE's own world transform at bind time — the same space a static mesh's vertices are
        // placed in via ModelMesh.Transform at render time. Skinned rendering has no such separate
        // slot (Transform3D.Identity is the convention — the skin already positions vertices in scene
        // space), so that node transform has to be baked into the vertex data itself instead, or the
        // mesh skins into the wrong space entirely (see Assets/Knight.NOTICE.md in Samples/SponzaNight
        // for the real asset — a Blender FBX export — that first exposed this).
        //
        // This is a minimal skinned glTF triangle: node 0 carries the mesh + skin plus a non-identity
        // scale of 2, one joint (node 1) at identity, one identity inverse bind matrix, and every
        // vertex fully weighted to that single joint — so at bind pose the skin palette is identity
        // and the only thing that can move a vertex from its authored (0,0,0)/(1,0,0)/(0,1,0)
        // positions is the fix under test.
        const string gltf = """
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [ { "nodes": [ 0, 1 ] } ],
              "nodes": [
                { "mesh": 0, "skin": 0, "scale": [ 2.0, 2.0, 2.0 ] },
                { "name": "joint" }
              ],
              "meshes": [
                {
                  "primitives": [
                    {
                      "attributes": { "POSITION": 0, "JOINTS_0": 2, "WEIGHTS_0": 3 },
                      "indices": 1
                    }
                  ]
                }
              ],
              "skins": [ { "inverseBindMatrices": 4, "joints": [ 1 ] } ],
              "buffers": [
                {
                  "byteLength": 180,
                  "uri": "data:application/octet-stream;base64,AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAABAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAAAAAAIA/"
                }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 },
                { "buffer": 0, "byteOffset": 44, "byteLength": 24, "target": 34962 },
                { "buffer": 0, "byteOffset": 68, "byteLength": 48, "target": 34962 },
                { "buffer": 0, "byteOffset": 116, "byteLength": 64 }
              ],
              "accessors": [
                {
                  "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3",
                  "min": [ 0.0, 0.0, 0.0 ], "max": [ 1.0, 1.0, 0.0 ]
                },
                { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" },
                { "bufferView": 2, "componentType": 5123, "count": 3, "type": "VEC4" },
                { "bufferView": 3, "componentType": 5126, "count": 3, "type": "VEC4" },
                { "bufferView": 4, "componentType": 5126, "count": 1, "type": "MAT4" }
              ]
            }
            """;
        var path = WriteTempObj(gltf, ".gltf");
        try
        {
            var scene = AssimpLoader.LoadScene(path);

            Assert.NotNull(scene.Skeleton);
            Assert.Single(scene.Meshes);

            var positions = scene.Meshes[0].Mesh.Vertices.Select(v => v.Position).ToArray();
            Assert.Contains(positions, p => IsClose(p, new Vector3(0f, 0f, 0f)));
            Assert.Contains(positions, p => IsClose(p, new Vector3(2f, 0f, 0f)));
            Assert.Contains(positions, p => IsClose(p, new Vector3(0f, 2f, 0f)));

            // The un-baked positions the fix must NOT leave in place — guards against a regression
            // that silently stops applying the node transform.
            Assert.DoesNotContain(positions, p => IsClose(p, new Vector3(1f, 0f, 0f)));
            Assert.DoesNotContain(positions, p => IsClose(p, new Vector3(0f, 1f, 0f)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool IsClose(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-3f;

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

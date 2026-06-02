using System.Numerics;
using Yaeger.Assets;

namespace Yaeger.Tests.Assets;

public class ObjLoaderTests
{
    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_SingleTriangle_ShouldReturnCorrectVertexPositionsAndIndices()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            g tri
            f 1//1 2//1 3//1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Single(meshes);
            var mesh = meshes[0].Mesh;
            Assert.Equal(3, mesh.Vertices.Length);
            Assert.Equal(3, mesh.Indices.Length);
            Assert.Equal(new Vector3(0f, 0f, 0f), mesh.Vertices[0].Position);
            Assert.Equal(new Vector3(1f, 0f, 0f), mesh.Vertices[1].Position);
            Assert.Equal(new Vector3(0f, 1f, 0f), mesh.Vertices[2].Position);
            Assert.Equal<uint>([0, 1, 2], mesh.Indices);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_QuadFace_ShouldFanTriangulateIntoTwoTriangles()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 1.0 1.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            g quad
            f 1//1 2//1 3//1 4//1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Single(meshes);
            var mesh = meshes[0].Mesh;
            Assert.Equal(4, mesh.Vertices.Length);
            // Two triangles = 6 indices
            Assert.Equal(6, mesh.Indices.Length);
            // First triangle: v0, v1, v2
            Assert.Equal<uint>(0, mesh.Indices[0]);
            Assert.Equal<uint>(1, mesh.Indices[1]);
            Assert.Equal<uint>(2, mesh.Indices[2]);
            // Second triangle: v0, v2, v3
            Assert.Equal<uint>(0, mesh.Indices[3]);
            Assert.Equal<uint>(2, mesh.Indices[4]);
            Assert.Equal<uint>(3, mesh.Indices[5]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MultiGroupObj_ShouldReturnCorrectNumberOfMeshes()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            v 0.0 0.0 1.0
            v 1.0 0.0 1.0
            v 0.0 1.0 1.0
            vn 0.0 0.0 1.0
            g groupA
            f 1//1 2//1 3//1
            g groupB
            f 4//1 5//1 6//1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Equal(2, meshes.Count);
            Assert.Equal("groupA", meshes[0].Name);
            Assert.Equal("groupB", meshes[1].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_UsemtlDirective_ShouldAssociateMaterialWithMesh()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            g mesh1
            usemtl myMaterial
            f 1//1 2//1 3//1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Single(meshes);
            Assert.Equal("myMaterial", meshes[0].MaterialName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithTextureCoordinates_ShouldSetTexCoords()
    {
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 0.0 1.0 0.0
            vt 0.0 0.0
            vt 1.0 0.0
            vt 0.0 1.0
            vn 0.0 0.0 1.0
            g tri
            f 1/1/1 2/2/1 3/3/1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Single(meshes);
            var vertices = meshes[0].Mesh.Vertices;
            Assert.Equal(new Vector2(0f, 0f), vertices[0].TexCoord);
            Assert.Equal(new Vector2(1f, 0f), vertices[1].TexCoord);
            Assert.Equal(new Vector2(0f, 1f), vertices[2].TexCoord);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_SharedVertices_ShouldDeduplicateViaCache()
    {
        // Two triangles sharing two vertices (a square split into triangles)
        var obj = """
            v 0.0 0.0 0.0
            v 1.0 0.0 0.0
            v 1.0 1.0 0.0
            v 0.0 1.0 0.0
            vn 0.0 0.0 1.0
            g shared
            f 1//1 2//1 3//1
            f 1//1 3//1 4//1
            """;
        var path = WriteTempFile(obj);

        try
        {
            var meshes = ObjLoader.Load(path);

            Assert.Single(meshes);
            var mesh = meshes[0].Mesh;
            // 4 unique (pos, norm) combinations → 4 vertices
            Assert.Equal(4, mesh.Vertices.Length);
            Assert.Equal(6, mesh.Indices.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

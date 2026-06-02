using System.Globalization;
using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Assets;

public static class ObjLoader
{
    public static IReadOnlyList<ObjMesh> Load(string objPath, string? mtlBasePath = null)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();

        var meshes = new List<ObjMesh>();

        string currentName = "default";
        string currentMaterial = "";
        var currentVertices = new List<Vertex3D>();
        var currentIndices = new List<uint>();
        var vertexCache = new Dictionary<(int posIdx, int texIdx, int normIdx), uint>();

        void FlushGroup()
        {
            if (currentIndices.Count == 0)
                return;
            var meshData = new MeshData(currentName, currentVertices.ToArray(), currentIndices.ToArray());
            meshes.Add(new ObjMesh(currentName, meshData, currentMaterial));
            currentVertices.Clear();
            currentIndices.Clear();
            vertexCache.Clear();
        }

        foreach (var rawLine in File.ReadLines(objPath))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var spaceIdx = line.IndexOf(' ');
            var keyword = spaceIdx < 0 ? line : line[..spaceIdx];
            var rest = spaceIdx < 0 ? "" : line[(spaceIdx + 1)..].TrimStart();

            switch (keyword)
            {
                case "v":
                {
                    var p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    positions.Add(
                        new Vector3(
                            float.Parse(p[0], CultureInfo.InvariantCulture),
                            float.Parse(p[1], CultureInfo.InvariantCulture),
                            float.Parse(p[2], CultureInfo.InvariantCulture)
                        )
                    );
                    break;
                }
                case "vn":
                {
                    var p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    normals.Add(
                        new Vector3(
                            float.Parse(p[0], CultureInfo.InvariantCulture),
                            float.Parse(p[1], CultureInfo.InvariantCulture),
                            float.Parse(p[2], CultureInfo.InvariantCulture)
                        )
                    );
                    break;
                }
                case "vt":
                {
                    var p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    texCoords.Add(
                        new Vector2(
                            float.Parse(p[0], CultureInfo.InvariantCulture),
                            float.Parse(p[1], CultureInfo.InvariantCulture)
                        )
                    );
                    break;
                }
                case "o":
                case "g":
                    FlushGroup();
                    currentName = rest;
                    break;
                case "usemtl":
                    currentMaterial = rest;
                    break;
                case "mtllib":
                    // Directive is recognized; callers use MtlLoader.Load() to retrieve material data.
                    break;
                case "f":
                {
                    var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var faceVerts = new uint[tokens.Length];
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        var (posIdx, texIdx, normIdx) = ParseFaceVertex(tokens[i]);
                        var key = (posIdx, texIdx, normIdx);
                        if (!vertexCache.TryGetValue(key, out var vertIdx))
                        {
                            var pos = positions[posIdx];
                            var norm = normIdx >= 0 ? normals[normIdx] : Vector3.UnitY;
                            var tex = texIdx >= 0 ? texCoords[texIdx] : Vector2.Zero;
                            vertIdx = (uint)currentVertices.Count;
                            currentVertices.Add(new Vertex3D(pos, norm, tex));
                            vertexCache[key] = vertIdx;
                        }
                        faceVerts[i] = vertIdx;
                    }
                    // Fan-triangulate: for n vertices emit (n-2) triangles
                    for (var i = 1; i < faceVerts.Length - 1; i++)
                    {
                        currentIndices.Add(faceVerts[0]);
                        currentIndices.Add(faceVerts[i]);
                        currentIndices.Add(faceVerts[i + 1]);
                    }
                    break;
                }
            }
        }

        FlushGroup();
        return meshes.AsReadOnly();
    }

    private static (int posIdx, int texIdx, int normIdx) ParseFaceVertex(string token)
    {
        var parts = token.Split('/');
        var posIdx = int.Parse(parts[0], CultureInfo.InvariantCulture) - 1;
        var texIdx = parts.Length > 1 && parts[1].Length > 0
            ? int.Parse(parts[1], CultureInfo.InvariantCulture) - 1
            : -1;
        var normIdx = parts.Length > 2 && parts[2].Length > 0
            ? int.Parse(parts[2], CultureInfo.InvariantCulture) - 1
            : -1;
        return (posIdx, texIdx, normIdx);
    }
}

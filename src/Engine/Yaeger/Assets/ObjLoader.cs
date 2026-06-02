using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Assets;

public static class ObjLoader
{
    public static IReadOnlyList<ObjMesh> Load(string objPath, string? mtlBasePath = null) =>
        LoadScene(objPath, mtlBasePath).Meshes;

    public static ObjScene LoadScene(string objPath, string? mtlBasePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objPath, nameof(objPath));
        if (mtlBasePath is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(mtlBasePath, nameof(mtlBasePath));

        var resolved = AssetPath.Resolve(objPath);

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"OBJ file not found: {objPath}", resolved);

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();

        var meshes = new List<ObjMesh>();
        var materials = new Dictionary<string, MtlMaterial>();

        string currentName = "default";
        string currentMaterial = "";
        var currentVertices = new List<Vertex3D>();
        var currentIndices = new List<uint>();
        var vertexCache = new Dictionary<(int posIdx, int texIdx, int normIdx), uint>();

        string mtlDir = mtlBasePath is not null
            ? AssetPath.Resolve(mtlBasePath)
            : Path.GetDirectoryName(resolved) ?? AppContext.BaseDirectory;

        void FlushGroup()
        {
            if (currentIndices.Count == 0)
                return;
            var meshData = new MeshData(
                currentName,
                currentVertices.ToArray(),
                currentIndices.ToArray()
            );
            meshes.Add(new ObjMesh(currentName, meshData, currentMaterial));
            currentVertices.Clear();
            currentIndices.Clear();
            vertexCache.Clear();
        }

        foreach (var rawLine in File.ReadLines(resolved))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var commentIdx = line.IndexOf('#');
            if (commentIdx >= 0)
                line = line[..commentIdx].TrimEnd();
            if (line.Length == 0)
                continue;

            var wsIdx = line.IndexOfAny(s_whitespace);
            var keyword = wsIdx < 0 ? line : line[..wsIdx];
            var rest = wsIdx < 0 ? "" : line[(wsIdx + 1)..].TrimStart();

            switch (keyword)
            {
                case "v":
                {
                    var p = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
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
                    var p = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
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
                    var p = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
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
                    FlushGroup();
                    currentMaterial = rest;
                    break;
                case "mtllib":
                {
                    var mtlPath = Path.Combine(mtlDir, rest);
                    if (File.Exists(mtlPath))
                    {
                        foreach (var (name, mat) in MtlLoader.Load(mtlPath))
                            materials[name] = mat;
                    }
                    break;
                }
                case "f":
                {
                    var tokens = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
                    var faceVerts = new uint[tokens.Length];
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        var (posIdx, texIdx, normIdx) = ParseFaceVertex(
                            tokens[i],
                            positions.Count,
                            texCoords.Count,
                            normals.Count
                        );
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
        return new ObjScene(
            meshes.AsReadOnly(),
            new ReadOnlyDictionary<string, MtlMaterial>(materials)
        );
    }

    private static readonly char[] s_whitespace = [' ', '\t'];

    private static (int posIdx, int texIdx, int normIdx) ParseFaceVertex(
        string token,
        int posCount,
        int texCount,
        int normCount
    )
    {
        var parts = token.Split('/');
        var posIdx = Resolve(int.Parse(parts[0], CultureInfo.InvariantCulture), posCount);
        var texIdx =
            parts.Length > 1 && parts[1].Length > 0
                ? Resolve(int.Parse(parts[1], CultureInfo.InvariantCulture), texCount)
                : -1;
        var normIdx =
            parts.Length > 2 && parts[2].Length > 0
                ? Resolve(int.Parse(parts[2], CultureInfo.InvariantCulture), normCount)
                : -1;
        return (posIdx, texIdx, normIdx);
    }

    private static int Resolve(int raw, int poolCount)
    {
        if (raw == 0)
            throw new FormatException("OBJ index 0 is invalid; indices are 1-based.");
        var idx = raw > 0 ? raw - 1 : poolCount + raw;
        if (idx < 0 || idx >= poolCount)
            throw new FormatException(
                $"OBJ index {raw} is out of range for pool of size {poolCount}."
            );
        return idx;
    }
}

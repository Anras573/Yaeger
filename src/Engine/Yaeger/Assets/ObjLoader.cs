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
                ComputeTangents(currentVertices, currentIndices),
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
                    if (p.Length < 3)
                        throw new FormatException(
                            $"OBJ 'v' requires 3 components; got {p.Length}."
                        );
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
                    if (p.Length < 3)
                        throw new FormatException(
                            $"OBJ 'vn' requires 3 components; got {p.Length}."
                        );
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
                    if (p.Length < 2)
                        throw new FormatException(
                            $"OBJ 'vt' requires at least 2 components; got {p.Length}."
                        );
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
                    currentName = rest.Length > 0 ? rest : "default";
                    break;
                case "usemtl":
                    FlushGroup();
                    currentMaterial = rest;
                    break;
                case "mtllib":
                {
                    var filenames = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
                    if (filenames.Length == 0)
                        throw new FormatException(
                            "OBJ 'mtllib' directive requires at least one filename."
                        );
                    var mtlDirFull = Path.GetFullPath(mtlDir);
                    foreach (var filename in filenames)
                    {
                        var mtlPath = Path.GetFullPath(Path.Combine(mtlDirFull, filename));
                        var relative = Path.GetRelativePath(mtlDirFull, mtlPath);
                        if (
                            Path.IsPathRooted(relative)
                            || relative.StartsWith("..", StringComparison.Ordinal)
                        )
                            throw new FormatException(
                                $"MTL filename '{filename}' resolves outside the MTL directory."
                            );
                        if (!File.Exists(mtlPath))
                            throw new FileNotFoundException(
                                $"MTL file '{filename}' referenced by OBJ not found.",
                                mtlPath
                            );
                        foreach (var (name, mat) in MtlLoader.Load(mtlPath))
                            materials[name] = mat;
                    }
                    break;
                }
                case "f":
                {
                    var tokens = rest.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 3)
                        throw new FormatException(
                            $"OBJ face 'f' requires at least 3 vertices; got {tokens.Length}."
                        );
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

    private static Vertex3D[] ComputeTangents(List<Vertex3D> vertices, List<uint> indices)
    {
        var tangents = new Vector3[vertices.Count];

        for (var i = 0; i < indices.Count; i += 3)
        {
            var i0 = (int)indices[i];
            var i1 = (int)indices[i + 1];
            var i2 = (int)indices[i + 2];

            var v0 = vertices[i0];
            var v1 = vertices[i1];
            var v2 = vertices[i2];

            var edge1 = v1.Position - v0.Position;
            var edge2 = v2.Position - v0.Position;
            var deltaUv1 = v1.TexCoord - v0.TexCoord;
            var deltaUv2 = v2.TexCoord - v0.TexCoord;

            var denom = deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y;
            var f = MathF.Abs(denom) > 1e-7f ? 1.0f / denom : 0f;

            var tangent = new Vector3(
                f * (deltaUv2.Y * edge1.X - deltaUv1.Y * edge2.X),
                f * (deltaUv2.Y * edge1.Y - deltaUv1.Y * edge2.Y),
                f * (deltaUv2.Y * edge1.Z - deltaUv1.Y * edge2.Z)
            );

            tangents[i0] += tangent;
            tangents[i1] += tangent;
            tangents[i2] += tangent;
        }

        var result = new Vertex3D[vertices.Count];
        for (var i = 0; i < vertices.Count; i++)
        {
            var t = tangents[i];
            var normalized = t.LengthSquared() > 1e-7f ? Vector3.Normalize(t) : Vector3.UnitX;
            var v = vertices[i];
            result[i] = new Vertex3D(v.Position, v.Normal, v.TexCoord, normalized);
        }

        return result;
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
        if (parts.Length > 3)
            throw new FormatException(
                $"OBJ face vertex '{token}' has {parts.Length} components; expected v, v/vt, v//vn, or v/vt/vn."
            );
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

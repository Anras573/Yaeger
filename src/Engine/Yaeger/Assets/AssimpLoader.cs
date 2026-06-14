using System.Numerics;
using Silk.NET.Assimp;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Assets;

public static class AssimpLoader
{
    public static ModelScene LoadScene(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        var resolved = AssetPath.Resolve(path);
        if (!System.IO.File.Exists(resolved))
            throw new FileNotFoundException($"Model file not found: {path}", resolved);

        var api = Assimp.GetApi();

        var flags =
            PostProcessSteps.Triangulate
            | PostProcessSteps.GenerateSmoothNormals
            | PostProcessSteps.CalculateTangentSpace
            | PostProcessSteps.JoinIdenticalVertices;

        unsafe
        {
            var scene = api.ImportFile(resolved, (uint)flags);

            if (scene == null)
                throw new InvalidOperationException(
                    $"Assimp failed to load '{path}': {api.GetErrorStringS()}"
                );

            try
            {
                const uint IncompleteFlag = 0x1u;
                if (scene->MRootNode == null || (scene->MFlags & IncompleteFlag) != 0)
                    throw new InvalidOperationException(
                        $"Assimp scene incomplete for '{path}': {api.GetErrorStringS()}"
                    );

                var baseDir = Path.GetDirectoryName(resolved) ?? AppContext.BaseDirectory;
                var meshes = new List<ModelMesh>();
                var meshDataCache = new Dictionary<uint, MeshData>();
                var materialCache = new Dictionary<uint, ModelMaterial>();
                ProcessNode(
                    api,
                    scene,
                    scene->MRootNode,
                    Matrix4x4.Identity,
                    baseDir,
                    meshes,
                    meshDataCache,
                    materialCache
                );
                return new ModelScene(meshes.AsReadOnly());
            }
            finally
            {
                api.FreeScene(scene);
            }
        }
    }

    private static unsafe void ProcessNode(
        Assimp api,
        Scene* scene,
        Node* node,
        Matrix4x4 parentTransform,
        string baseDir,
        List<ModelMesh> meshes,
        Dictionary<uint, MeshData> meshDataCache,
        Dictionary<uint, ModelMaterial> materialCache
    )
    {
        // Assimp uses column-vector (OpenGL) convention; System.Numerics uses row-vector.
        // Transpose converts between them.
        var nodeTransform = Matrix4x4.Transpose(node->MTransformation);
        var worldTransform = nodeTransform * parentTransform;

        for (var i = 0u; i < node->MNumMeshes; i++)
        {
            var meshIdx = node->MMeshes[i];
            var assimpMesh = scene->MMeshes[meshIdx];
            if (!meshDataCache.TryGetValue(meshIdx, out var meshData))
            {
                meshData = ExtractMeshData(assimpMesh);
                meshDataCache[meshIdx] = meshData;
            }

            var matIdx = assimpMesh->MMaterialIndex;
            if (!materialCache.TryGetValue(matIdx, out var material))
            {
                material = ExtractMaterial(api, scene->MMaterials[matIdx], baseDir);
                materialCache[matIdx] = material;
            }

            if (
                !Matrix4x4.Decompose(
                    worldTransform,
                    out var scale,
                    out var rotation,
                    out var translation
                )
            )
            {
                scale = Vector3.One;
                rotation = Quaternion.Identity;
                translation = Vector3.Zero;
            }
            var transform = new Transform3D(translation, rotation, scale);

            var nodeName = node->MName.AsString;
            var name = string.IsNullOrEmpty(nodeName) ? assimpMesh->MName.AsString : nodeName;
            meshes.Add(new ModelMesh(name, meshData, material, transform));
        }

        for (var i = 0u; i < node->MNumChildren; i++)
            ProcessNode(
                api,
                scene,
                node->MChildren[i],
                worldTransform,
                baseDir,
                meshes,
                meshDataCache,
                materialCache
            );
    }

    private static unsafe MeshData ExtractMeshData(Mesh* mesh)
    {
        var vertCount = mesh->MNumVertices;
        var vertices = new Vertex3D[vertCount];
        var uvChannel0 = mesh->MTextureCoords.Element0;
        var hasTangents = mesh->MTangents != null;

        for (var i = 0u; i < vertCount; i++)
        {
            var pos = mesh->MVertices[i];
            var norm = mesh->MNormals != null ? mesh->MNormals[i] : Vector3.UnitY;
            var uv =
                uvChannel0 != null ? new Vector2(uvChannel0[i].X, uvChannel0[i].Y) : Vector2.Zero;
            var tangent = hasTangents ? mesh->MTangents[i] : Vector3.Zero;

            vertices[i] = new Vertex3D(pos, norm, uv, tangent);
        }

        var indices = new List<uint>((int)mesh->MNumFaces * 3);
        for (var i = 0u; i < mesh->MNumFaces; i++)
        {
            var face = mesh->MFaces[i];
            for (var j = 0u; j < face.MNumIndices; j++)
                indices.Add(face.MIndices[j]);
        }

        return new MeshData(mesh->MName.AsString, vertices, indices.ToArray());
    }

    private static unsafe ModelMaterial ExtractMaterial(Assimp api, Material* mat, string baseDir)
    {
        AssimpString nameStr = default;
        api.GetMaterialString(mat, Assimp.MaterialNameBase, 0, 0, &nameStr);
        var name = nameStr.AsString;

        var diffusePath = GetTexturePath(api, mat, TextureType.Diffuse, baseDir);
        var normalPath = GetTexturePath(api, mat, TextureType.Normals, baseDir);

        var diffuseColor = Color.White;
        var col = Vector4.One;
        var colResult = api.GetMaterialColor(mat, Assimp.MaterialColorDiffuseBase, 0, 0, ref col);
        if (colResult == Return.Success)
            diffuseColor = Color.FromVector4(col);

        Color ambientColor;
        var amb = Vector4.Zero;
        var ambResult = api.GetMaterialColor(mat, Assimp.MaterialColorAmbientBase, 0, 0, ref amb);
        if (ambResult == Return.Success && (amb.X + amb.Y + amb.Z) > 0f)
            ambientColor = Color.FromVector4(amb with { W = 1f });
        else
            // Fall back to ~25% grey so unlit faces remain visible.
            ambientColor = new Color(64, 64, 64, 255);

        // --- PBR metallic/roughness (glTF 2.0) ---
        // Assimp's glTF importer assigns the packed metallic-roughness texture to METALNESS,
        // the occlusion texture to AMBIENT_OCCLUSION (Lightmap on older builds), and the
        // emissive texture to EMISSIVE.
        var metallicRoughnessPath = GetTexturePath(api, mat, TextureType.Metalness, baseDir);
        var aoPath =
            GetTexturePath(api, mat, TextureType.AmbientOcclusion, baseDir)
            ?? GetTexturePath(api, mat, TextureType.Lightmap, baseDir);
        var emissivePath = GetTexturePath(api, mat, TextureType.Emissive, baseDir);

        var hasMetallic = TryGetMaterialFloat(
            api,
            mat,
            Assimp.MatkeyMetallicFactor,
            out var metallicFactor
        );
        var hasRoughness = TryGetMaterialFloat(
            api,
            mat,
            Assimp.MatkeyRoughnessFactor,
            out var roughnessFactor
        );

        var emissiveColor = Color.Black;
        var emissive = Vector4.Zero;
        var emissiveResult = api.GetMaterialColor(
            mat,
            Assimp.MaterialColorEmissiveBase,
            0,
            0,
            ref emissive
        );
        if (emissiveResult == Return.Success)
            emissiveColor = Color.FromVector4(emissive with { W = 1f });

        // Treat the material as PBR when the importer surfaced metallic/roughness data — glTF
        // always provides these (the factor keys, and a metalness texture slot), whereas OBJ/MTL
        // (Blinn-Phong) never does. Emissive/AO are deliberately excluded: OBJ can carry an
        // emissive map/colour, which must not flip an otherwise Blinn-Phong material to PBR.
        var usePbr = hasMetallic || hasRoughness || metallicRoughnessPath != null;

        return new ModelMaterial(
            name,
            diffusePath,
            normalPath,
            diffuseColor,
            ambientColor,
            metallicRoughnessPath,
            aoPath,
            emissivePath,
            hasMetallic ? metallicFactor : 1f,
            hasRoughness ? roughnessFactor : 1f,
            emissiveColor,
            usePbr
        );
    }

    private static unsafe string? GetTexturePath(
        Assimp api,
        Material* mat,
        TextureType type,
        string baseDir
    )
    {
        AssimpString pathStr = default;
        var result = api.GetMaterialTexture(
            mat,
            type,
            0,
            &pathStr,
            null,
            null,
            null,
            null,
            null,
            null
        );
        if (result == Return.Success && pathStr.Length > 0)
            return Path.GetFullPath(Path.Combine(baseDir, pathStr.AsString));

        return null;
    }

    private static unsafe bool TryGetMaterialFloat(
        Assimp api,
        Material* mat,
        string key,
        out float value
    )
    {
        float result = 0f;
        uint max = 1;
        var status = api.GetMaterialFloatArray(mat, key, 0, 0, &result, &max);
        value = result;
        // `max` is updated to the number of values actually written; require at least one so a
        // present-but-empty property doesn't masquerade as a real 0.0 factor.
        return status == Return.Success && max >= 1;
    }
}

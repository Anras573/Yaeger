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
                ProcessNode(api, scene, scene->MRootNode, Matrix4x4.Identity, baseDir, meshes);
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
        List<ModelMesh> meshes
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
            var meshData = ExtractMeshData(assimpMesh);
            var material = ExtractMaterial(
                api,
                scene->MMaterials[assimpMesh->MMaterialIndex],
                baseDir
            );

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

            meshes.Add(new ModelMesh(assimpMesh->MName.AsString, meshData, material, transform));
        }

        for (var i = 0u; i < node->MNumChildren; i++)
            ProcessNode(api, scene, node->MChildren[i], worldTransform, baseDir, meshes);
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

        string? diffusePath = null;
        AssimpString diffuseStr = default;
        var diffuseResult = api.GetMaterialTexture(
            mat,
            TextureType.Diffuse,
            0,
            &diffuseStr,
            null,
            null,
            null,
            null,
            null,
            null
        );
        if (diffuseResult == Return.Success && diffuseStr.Length > 0)
        {
            diffusePath = Path.GetFullPath(Path.Combine(baseDir, diffuseStr.AsString));
        }

        string? normalPath = null;
        AssimpString normalStr = default;
        var normalResult = api.GetMaterialTexture(
            mat,
            TextureType.Normals,
            0,
            &normalStr,
            null,
            null,
            null,
            null,
            null,
            null
        );
        if (normalResult == Return.Success && normalStr.Length > 0)
        {
            normalPath = Path.GetFullPath(Path.Combine(baseDir, normalStr.AsString));
        }

        var diffuseColor = Color.White;
        var col = Vector4.One;
        var colResult = api.GetMaterialColor(mat, Assimp.MaterialColorDiffuseBase, 0, 0, ref col);
        if (colResult == Return.Success)
        {
            diffuseColor = new Color(
                (byte)Math.Clamp((int)(col.X * 255f), 0, 255),
                (byte)Math.Clamp((int)(col.Y * 255f), 0, 255),
                (byte)Math.Clamp((int)(col.Z * 255f), 0, 255),
                (byte)Math.Clamp((int)(col.W * 255f), 0, 255)
            );
        }

        return new ModelMaterial(name, diffusePath, normalPath, diffuseColor);
    }
}

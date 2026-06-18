using System.Numerics;
using Silk.NET.Assimp;
using Yaeger.Graphics;
using Yaeger.Rendering;
// Disambiguate the engine's animation types from Assimp's identically-named native structs, which
// are still referenced through Scene*/Mesh* pointer fields elsewhere in this file.
using Bone = Yaeger.Graphics.Bone;
using Skeleton = Yaeger.Graphics.Skeleton;
using VectorKey = Yaeger.Graphics.VectorKey;

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

                // Build the node hierarchy up front: vertex bone indices and animation channels both
                // reference nodes by name, so we need a stable node -> index map (and parent links)
                // before extracting any skinning data. Pre-order traversal guarantees a parent's
                // index precedes its children's, which the skinning palette pass relies on.
                var nodes = new List<(string Name, int ParentIndex, Matrix4x4 LocalTransform)>();
                var nameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                BuildNodeHierarchy(scene->MRootNode, -1, nodes, nameToIndex);

                var inverseBindPoses = new Matrix4x4[nodes.Count];
                Array.Fill(inverseBindPoses, Matrix4x4.Identity);
                var hasSkin = false;

                ProcessNode(
                    api,
                    scene,
                    scene->MRootNode,
                    Matrix4x4.Identity,
                    baseDir,
                    meshes,
                    meshDataCache,
                    materialCache,
                    nameToIndex,
                    inverseBindPoses,
                    ref hasSkin
                );

                // Only surface a skeleton/animations for actually-skinned models; static meshes
                // (e.g. OBJ) leave these null/empty so existing consumers are unaffected.
                Skeleton? skeleton = null;
                IReadOnlyList<AnimationClip>? animations = null;
                if (hasSkin)
                {
                    var bones = new Bone[nodes.Count];
                    for (var i = 0; i < nodes.Count; i++)
                        bones[i] = new Bone(
                            nodes[i].Name,
                            nodes[i].ParentIndex,
                            nodes[i].LocalTransform
                        );
                    skeleton = new Skeleton(bones, inverseBindPoses);
                    animations = ExtractAnimations(scene, nameToIndex);
                }

                return new ModelScene(meshes.AsReadOnly(), skeleton, animations);
            }
            finally
            {
                api.FreeScene(scene);
            }
        }
    }

    // Recursively walks the node tree, building the global node-name -> bone-index map and recording
    // parent links in pre-order. Skinning data is keyed off these indices.
    private static unsafe void BuildNodeHierarchy(
        Node* node,
        int parentIndex,
        List<(string Name, int ParentIndex, Matrix4x4 LocalTransform)> nodes,
        Dictionary<string, int> nameToIndex
    )
    {
        var name = node->MName.AsString;
        // Assimp uses column-vector convention; transpose into System.Numerics' row-vector layout.
        var localTransform = Matrix4x4.Transpose(node->MTransformation);
        var index = nodes.Count;
        nodes.Add((name, parentIndex, localTransform));

        // Keep the first occurrence if names collide (rare); bone/channel lookups expect uniqueness.
        if (!string.IsNullOrEmpty(name))
            nameToIndex.TryAdd(name, index);

        for (var i = 0u; i < node->MNumChildren; i++)
            BuildNodeHierarchy(node->MChildren[i], index, nodes, nameToIndex);
    }

    private static unsafe void ProcessNode(
        Assimp api,
        Scene* scene,
        Node* node,
        Matrix4x4 parentTransform,
        string baseDir,
        List<ModelMesh> meshes,
        Dictionary<uint, MeshData> meshDataCache,
        Dictionary<uint, ModelMaterial> materialCache,
        Dictionary<string, int> nameToIndex,
        Matrix4x4[] inverseBindPoses,
        ref bool hasSkin
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
                meshData = ExtractMeshData(assimpMesh, nameToIndex, inverseBindPoses, ref hasSkin);
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
                materialCache,
                nameToIndex,
                inverseBindPoses,
                ref hasSkin
            );
    }

    private static unsafe MeshData ExtractMeshData(
        Mesh* mesh,
        Dictionary<string, int> nameToIndex,
        Matrix4x4[] inverseBindPoses,
        ref bool hasSkin
    )
    {
        var vertCount = mesh->MNumVertices;
        var vertices = new Vertex3D[vertCount];
        var uvChannel0 = mesh->MTextureCoords.Element0;
        var hasTangents = mesh->MTangents != null;

        // Gather per-vertex bone influences (up to 4) when the mesh is skinned. Assimp stores skin
        // weights per bone (each bone lists the vertices it affects); invert that into per-vertex.
        var influences = ExtractInfluences(mesh, nameToIndex, inverseBindPoses, ref hasSkin);

        for (var i = 0u; i < vertCount; i++)
        {
            var pos = mesh->MVertices[i];
            var norm = mesh->MNormals != null ? mesh->MNormals[i] : Vector3.UnitY;
            var uv =
                uvChannel0 != null ? new Vector2(uvChannel0[i].X, uvChannel0[i].Y) : Vector2.Zero;
            var tangent = hasTangents ? mesh->MTangents[i] : Vector3.Zero;

            var boneIndices = influences != null ? influences[i].Indices : Vector4.Zero;
            var boneWeights = influences != null ? influences[i].NormalizedWeights() : Vector4.Zero;

            vertices[i] = new Vertex3D(pos, norm, uv, tangent, boneIndices, boneWeights);
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

    // Builds per-vertex bone influences and records each bone's inverse bind pose. Returns null for
    // non-skinned meshes so the caller can fall back to identity skinning. Sets hasSkin when any
    // bone is present.
    private static unsafe VertexInfluences[]? ExtractInfluences(
        Mesh* mesh,
        Dictionary<string, int> nameToIndex,
        Matrix4x4[] inverseBindPoses,
        ref bool hasSkin
    )
    {
        if (mesh->MNumBones == 0)
            return null;

        hasSkin = true;
        var influences = new VertexInfluences[mesh->MNumVertices];

        for (var b = 0u; b < mesh->MNumBones; b++)
        {
            var bone = mesh->MBones[b];
            var name = bone->MName.AsString;
            if (!nameToIndex.TryGetValue(name, out var boneIndex))
                continue;

            // The offset matrix transforms mesh space into this bone's space (the inverse bind pose).
            inverseBindPoses[boneIndex] = Matrix4x4.Transpose(bone->MOffsetMatrix);

            for (var w = 0u; w < bone->MNumWeights; w++)
            {
                var weight = bone->MWeights[w];
                if (weight.MVertexId < influences.Length)
                    influences[weight.MVertexId].Add(boneIndex, weight.MWeight);
            }
        }

        return influences;
    }

    private static unsafe IReadOnlyList<AnimationClip> ExtractAnimations(
        Scene* scene,
        Dictionary<string, int> nameToIndex
    )
    {
        var clips = new List<AnimationClip>((int)scene->MNumAnimations);

        for (var a = 0u; a < scene->MNumAnimations; a++)
        {
            var anim = scene->MAnimations[a];
            // Assimp expresses key times in "ticks"; convert to seconds. Default to 25 ticks/sec
            // (Assimp's convention) when the rate is unspecified.
            var ticksPerSecond = anim->MTicksPerSecond != 0.0 ? anim->MTicksPerSecond : 25.0;
            var duration = (float)(anim->MDuration / ticksPerSecond);

            var tracks = new List<BoneTrack>((int)anim->MNumChannels);
            for (var c = 0u; c < anim->MNumChannels; c++)
            {
                var channel = anim->MChannels[c];
                var nodeName = channel->MNodeName.AsString;
                if (!nameToIndex.TryGetValue(nodeName, out var boneIndex))
                    continue;

                var positions = new VectorKey[channel->MNumPositionKeys];
                for (var k = 0u; k < channel->MNumPositionKeys; k++)
                {
                    var key = channel->MPositionKeys[k];
                    positions[k] = new VectorKey((float)(key.MTime / ticksPerSecond), key.MValue);
                }

                var rotations = new QuaternionKey[channel->MNumRotationKeys];
                for (var k = 0u; k < channel->MNumRotationKeys; k++)
                {
                    var key = channel->MRotationKeys[k];
                    rotations[k] = new QuaternionKey(
                        (float)(key.MTime / ticksPerSecond),
                        key.MValue
                    );
                }

                var scales = new VectorKey[channel->MNumScalingKeys];
                for (var k = 0u; k < channel->MNumScalingKeys; k++)
                {
                    var key = channel->MScalingKeys[k];
                    scales[k] = new VectorKey((float)(key.MTime / ticksPerSecond), key.MValue);
                }

                tracks.Add(new BoneTrack(boneIndex, positions, rotations, scales));
            }

            var name = anim->MName.AsString;
            clips.Add(
                new AnimationClip(
                    string.IsNullOrEmpty(name) ? $"animation{a}" : name,
                    duration,
                    tracks.ToArray()
                )
            );
        }

        return clips;
    }

    // Accumulates up to four bone influences for a single vertex, keeping the heaviest weights. Bone
    // indices are stored as floats (the GPU reads them via vec4 attributes).
    private struct VertexInfluences
    {
        private Vector4 _indices;
        private Vector4 _weights;
        private int _count;

        public readonly Vector4 Indices => _indices;

        public void Add(int boneIndex, float weight)
        {
            if (weight <= 0f)
                return;

            if (_count < 4)
            {
                SetSlot(_count, boneIndex, weight);
                _count++;
                return;
            }

            // Replace the smallest existing influence when this one is heavier.
            var minSlot = 0;
            var minWeight = _weights.X;
            if (_weights.Y < minWeight)
            {
                minWeight = _weights.Y;
                minSlot = 1;
            }
            if (_weights.Z < minWeight)
            {
                minWeight = _weights.Z;
                minSlot = 2;
            }
            if (_weights.W < minWeight)
            {
                minWeight = _weights.W;
                minSlot = 3;
            }
            if (weight > minWeight)
                SetSlot(minSlot, boneIndex, weight);
        }

        public readonly Vector4 NormalizedWeights()
        {
            var sum = _weights.X + _weights.Y + _weights.Z + _weights.W;
            return sum > 0f ? _weights / sum : _weights;
        }

        private void SetSlot(int slot, int boneIndex, float weight)
        {
            switch (slot)
            {
                case 0:
                    _indices.X = boneIndex;
                    _weights.X = weight;
                    break;
                case 1:
                    _indices.Y = boneIndex;
                    _weights.Y = weight;
                    break;
                case 2:
                    _indices.Z = boneIndex;
                    _weights.Z = weight;
                    break;
                default:
                    _indices.W = boneIndex;
                    _weights.W = weight;
                    break;
            }
        }
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

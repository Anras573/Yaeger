#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aTangent;
layout(location = 4) in vec4 aBoneIndices;
layout(location = 5) in vec4 aBoneWeights;

// Per-instance model/normal matrices (VertexAttribDivisor 1), populated by GpuMesh.DrawInstanced.
// Only read when uInstanced != 0; harmless/unused otherwise, mirroring how the skinning attributes
// above are harmless/unused when uSkinned == 0.
layout(location = 6) in mat4 aInstanceModel;
layout(location = 10) in mat3 aInstanceNormalMatrix;

// Per-instance bone-palette base offset for instanced skinning (VertexAttribDivisor 1), populated
// by GpuMesh.DrawInstancedSkinned. Only read when uInstanced != 0 && uSkinned != 0.
layout(location = 13) in float aInstancePaletteBase;

uniform mat4 uModel;
uniform mat4 uViewProj;
uniform mat3 uNormalMatrix;
uniform mat4 uLightSpaceMatrix;
uniform int uInstanced;

// GPU skinning: a palette of bone matrices, either the fixed-size uniform buffer below (immediate,
// non-instanced draws) or the per-instance texture buffer further down (instanced draws - a crowd of
// characters can't share one 128-matrix UBO, since each instance has its own pose). uSkinned gates
// the whole path so static meshes (all weights zero) are unaffected; uInstanced picks which palette
// source an individual skinned vertex reads.
const int MAX_BONES = 128;
layout(std140) uniform Bones {
    mat4 uBones[MAX_BONES];
};
uniform int uSkinned;

// Instanced skinning's bone-palette texture buffer: every drawn instance's resolved skinning
// matrices, packed back-to-back by Renderer3D.DrawInstancedSkinned (see BonePaletteBuffer), each
// bone occupying 4 consecutive texels (its columns, raw-memory order - the same convention
// SetBoneMatrices already uses for the UBO above). aInstancePaletteBase is this instance's starting
// bone slot; texel index = (aInstancePaletteBase + boneIndex) * 4 + column.
uniform samplerBuffer uBonePalette;

mat4 fetchInstanceBone(int slot) {
    int base = slot * 4;
    vec4 c0 = texelFetch(uBonePalette, base + 0);
    vec4 c1 = texelFetch(uBonePalette, base + 1);
    vec4 c2 = texelFetch(uBonePalette, base + 2);
    vec4 c3 = texelFetch(uBonePalette, base + 3);
    return mat4(c0, c1, c2, c3);
}

out vec3 vNormal;
out vec2 vTexCoord;
out vec3 vFragPos;
out vec3 vTangent;
out vec4 vLightSpacePos;

void main() {
    mat4 model = uInstanced != 0 ? aInstanceModel : uModel;
    mat3 normalMatrix = uInstanced != 0 ? aInstanceNormalMatrix : uNormalMatrix;

    mat4 skin = mat4(1.0);
    if (uSkinned != 0) {
        float wSum = dot(aBoneWeights, vec4(1.0));
        if (wSum > 1e-4) {
            if (uInstanced != 0) {
                // The instanced palette buffer is sized exactly to this draw's instances (see
                // InstancedSkinningPlanner), so every in-range bone index is valid by construction -
                // no bounds guard needed here, unlike the fixed MAX_BONES UBO below.
                int base = int(aInstancePaletteBase);
                skin =
                    fetchInstanceBone(base + int(aBoneIndices.x)) * aBoneWeights.x +
                    fetchInstanceBone(base + int(aBoneIndices.y)) * aBoneWeights.y +
                    fetchInstanceBone(base + int(aBoneIndices.z)) * aBoneWeights.z +
                    fetchInstanceBone(base + int(aBoneIndices.w)) * aBoneWeights.w;
            } else {
                // Guard against out-of-range indices (e.g. a model with more bones than the palette
                // holds): an OOB uBones[] read is undefined behaviour. Fall back to identity skin
                // (bind pose) when any of the four indices is outside [0, MAX_BONES).
                bool inRange =
                    all(greaterThanEqual(aBoneIndices, vec4(0.0))) &&
                    all(lessThan(aBoneIndices, vec4(float(MAX_BONES))));
                if (inRange) {
                    skin =
                        uBones[int(aBoneIndices.x)] * aBoneWeights.x +
                        uBones[int(aBoneIndices.y)] * aBoneWeights.y +
                        uBones[int(aBoneIndices.z)] * aBoneWeights.z +
                        uBones[int(aBoneIndices.w)] * aBoneWeights.w;
                }
            }
        }
    }

    vec4 skinnedPos = skin * vec4(aPosition, 1.0);
    mat3 skin3 = mat3(skin);
    // Normals need the inverse-transpose of the skin matrix so non-uniform bone scale doesn't
    // skew them; tangents are surface directions and use the skin matrix directly. Both are
    // identity for static meshes (skin == identity).
    mat3 skinNormal = transpose(inverse(skin3));

    vec4 worldPos = model * skinnedPos;
    vFragPos  = worldPos.xyz;
    vNormal   = normalMatrix * (skinNormal * aNormal);
    vTangent  = mat3(model) * (skin3 * aTangent);
    vTexCoord = aTexCoord;
    vLightSpacePos = uLightSpaceMatrix * worldPos;
    gl_Position = uViewProj * worldPos;
}

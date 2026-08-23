#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 4) in vec4 aBoneIndices;
layout(location = 5) in vec4 aBoneWeights;

// Same attribute location as Renderer3D.vert's aInstanceModel, populated by the same
// GpuMesh.DrawInstanced path; only read when uInstanced != 0.
layout(location = 6) in mat4 aInstanceModel;

// Same attribute location as Renderer3D.vert's aInstancePaletteBase, populated by
// GpuMesh.DrawInstancedSkinned; only read when uInstanced != 0 && uSkinned != 0.
layout(location = 13) in float aInstancePaletteBase;

uniform mat4 uLightSpace;
uniform mat4 uModel;
uniform int uInstanced;

// GPU skinning: same palette/gating scheme as Renderer3D.vert (uSkinned gates the whole path, an
// out-of-range bone index falls back to identity skin for the immediate UBO path). Only position
// needs skinning here - the depth-only pass never reads normals/tangents.
const int MAX_BONES = 128;
layout(std140) uniform Bones {
    mat4 uBones[MAX_BONES];
};
uniform int uSkinned;

// Instanced skinning's bone-palette texture buffer — same layout/convention as Renderer3D.vert's
// uBonePalette, but this shadow shader's own texture buffer (ShadowMapRenderer keeps its bone data
// independent of Renderer3D's, same as the UBO above already does).
uniform samplerBuffer uBonePalette;

mat4 fetchInstanceBone(int slot) {
    int base = slot * 4;
    vec4 c0 = texelFetch(uBonePalette, base + 0);
    vec4 c1 = texelFetch(uBonePalette, base + 1);
    vec4 c2 = texelFetch(uBonePalette, base + 2);
    vec4 c3 = texelFetch(uBonePalette, base + 3);
    return mat4(c0, c1, c2, c3);
}

void main() {
    mat4 model = uInstanced != 0 ? aInstanceModel : uModel;

    mat4 skin = mat4(1.0);
    if (uSkinned != 0) {
        float wSum = dot(aBoneWeights, vec4(1.0));
        if (wSum > 1e-4) {
            if (uInstanced != 0) {
                int base = int(aInstancePaletteBase);
                skin =
                    fetchInstanceBone(base + int(aBoneIndices.x)) * aBoneWeights.x +
                    fetchInstanceBone(base + int(aBoneIndices.y)) * aBoneWeights.y +
                    fetchInstanceBone(base + int(aBoneIndices.z)) * aBoneWeights.z +
                    fetchInstanceBone(base + int(aBoneIndices.w)) * aBoneWeights.w;
            } else {
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

    gl_Position = uLightSpace * model * skin * vec4(aPosition, 1.0);
}

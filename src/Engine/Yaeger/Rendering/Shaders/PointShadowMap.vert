#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 4) in vec4 aBoneIndices;
layout(location = 5) in vec4 aBoneWeights;

// Same attribute location as Renderer3D.vert's aInstanceModel, populated by the same
// GpuMesh.DrawInstanced path; only read when uInstanced != 0.
layout(location = 6) in mat4 aInstanceModel;

// Combined view*projection for the cube face currently being captured (see
// PointShadowMapRenderer.ComputeFaceViewProjection) — same "one combined matrix" shape as
// ShadowMap.vert's uLightSpace, just recomputed per face instead of once per light.
uniform mat4 uLightSpace;
uniform mat4 uModel;
uniform int uInstanced;

// GPU skinning: identical scheme to ShadowMap.vert (uSkinned gates the whole path, an
// out-of-range bone index falls back to identity skin). Only position needs skinning here — the
// depth-only pass never reads normals/tangents.
const int MAX_BONES = 128;
layout(std140) uniform Bones {
    mat4 uBones[MAX_BONES];
};
uniform int uSkinned;

// World-space position, for the fragment shader to measure distance from the light — gl_Position
// alone (clip space) can't recover this.
out vec3 vWorldPos;

void main() {
    mat4 model = uInstanced != 0 ? aInstanceModel : uModel;

    mat4 skin = mat4(1.0);
    if (uSkinned != 0) {
        float wSum = dot(aBoneWeights, vec4(1.0));
        bool inRange =
            all(greaterThanEqual(aBoneIndices, vec4(0.0))) &&
            all(lessThan(aBoneIndices, vec4(float(MAX_BONES))));
        if (wSum > 1e-4 && inRange) {
            skin =
                uBones[int(aBoneIndices.x)] * aBoneWeights.x +
                uBones[int(aBoneIndices.y)] * aBoneWeights.y +
                uBones[int(aBoneIndices.z)] * aBoneWeights.z +
                uBones[int(aBoneIndices.w)] * aBoneWeights.w;
        }
    }

    vec4 worldPos = model * skin * vec4(aPosition, 1.0);
    vWorldPos = worldPos.xyz;
    gl_Position = uLightSpace * worldPos;
}

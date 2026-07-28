#version 330 core
layout(location = 0) in vec3 aPosition;

// Same attribute location as Renderer3D.vert's aInstanceModel, populated by the same
// GpuMesh.DrawInstanced path; only read when uInstanced != 0.
layout(location = 6) in mat4 aInstanceModel;

uniform mat4 uLightSpace;
uniform mat4 uModel;
uniform int uInstanced;

void main() {
    mat4 model = uInstanced != 0 ? aInstanceModel : uModel;
    gl_Position = uLightSpace * model * vec4(aPosition, 1.0);
}

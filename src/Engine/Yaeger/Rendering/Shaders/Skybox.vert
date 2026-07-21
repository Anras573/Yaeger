#version 330 core
layout(location = 0) in vec3 aPosition;

uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vTexCoords;

void main() {
    vTexCoords  = aPosition;
    vec4 pos    = uProjection * uView * vec4(aPosition, 1.0);
    gl_Position = pos.xyww;
}

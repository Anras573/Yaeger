#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uSource;

void main() {
    FragColor = texture(uSource, vTexCoord);
}

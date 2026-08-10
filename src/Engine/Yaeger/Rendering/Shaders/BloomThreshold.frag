#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uSource;
uniform float uThreshold;
uniform float uSoftKnee;

void main() {
    vec3 color = texture(uSource, vTexCoord).rgb;
    float brightness = max(color.r, max(color.g, color.b));
    float contribution = smoothstep(uThreshold, uThreshold + max(uSoftKnee, 1e-4), brightness);
    FragColor = vec4(color * contribution, 1.0);
}

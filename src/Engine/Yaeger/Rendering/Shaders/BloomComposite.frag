#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uScene;
uniform sampler2D uBloom;
uniform float uIntensity;

void main() {
    vec3 scene = texture(uScene, vTexCoord).rgb;
    vec3 bloom = texture(uBloom, vTexCoord).rgb;
    FragColor = vec4(scene + bloom * uIntensity, 1.0);
}

#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uSource;
uniform float uIntensity; // 0 = no darkening at the edges, 1 = fully black at the edges
uniform float uRadius; // distance from the centre (in UV units) where darkening starts
uniform float uSoftness; // width of the fade from full brightness to uIntensity
uniform float uSaturation; // 1 = unchanged, 0 = grayscale, >1 = boosted
uniform vec3 uTint; // multiplicative colour tint, (1,1,1) = neutral

void main() {
    vec4 color = texture(uSource, vTexCoord);

    float dist = length(vTexCoord - vec2(0.5));
    float vignette = 1.0 - uIntensity * smoothstep(uRadius, uRadius + uSoftness, dist);

    float luminance = dot(color.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 graded = mix(vec3(luminance), color.rgb, uSaturation) * uTint;

    FragColor = vec4(graded * vignette, color.a);
}

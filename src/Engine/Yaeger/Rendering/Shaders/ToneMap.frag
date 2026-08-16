#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uSource;
uniform float uExposure;
uniform int uOperator; // 0 = Reinhard, 1 = ACES filmic

vec3 reinhard(vec3 c) {
    return c / (c + vec3(1.0));
}

// Narkowicz 2015 ACES filmic fit ("ACES Filmic Tone Mapping Curve").
vec3 acesFilmic(vec3 c) {
    const float a = 2.51;
    const float b = 0.03;
    const float cc = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((c * (a * c + b)) / (c * (cc * c + d) + e), 0.0, 1.0);
}

void main() {
    vec3 hdr = texture(uSource, vTexCoord).rgb * max(uExposure, 0.0);
    vec3 mapped = uOperator == 1 ? acesFilmic(hdr) : reinhard(hdr);
    mapped = pow(clamp(mapped, 0.0, 1.0), vec3(1.0 / 2.2));
    FragColor = vec4(mapped, 1.0);
}

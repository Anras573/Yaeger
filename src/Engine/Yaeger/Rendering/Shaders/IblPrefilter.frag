#version 330 core
in  vec3 vLocalPos;
out vec4 FragColor;

uniform samplerCube uSource;
uniform float uRoughness;

IMPORTANCE_SAMPLING_GLSL

void main() {
    vec3 N = normalize(vLocalPos);
    vec3 R = N;
    vec3 V = R;

    const uint SAMPLE_COUNT = 64u;
    vec3 prefilteredColor = vec3(0.0);
    float totalWeight = 0.0;

    for (uint i = 0u; i < SAMPLE_COUNT; i++) {
        vec2 Xi = hammersley(i, SAMPLE_COUNT);
        vec3 H = importanceSampleGGX(Xi, N, uRoughness);
        vec3 L = normalize(2.0 * dot(V, H) * H - V);

        float NdotL = max(dot(N, L), 0.0);
        if (NdotL > 0.0) {
            vec3 radiance = pow(texture(uSource, L).rgb, vec3(2.2));
            prefilteredColor += radiance * NdotL;
            totalWeight += NdotL;
        }
    }
    prefilteredColor = totalWeight > 0.0 ? prefilteredColor / totalWeight : vec3(0.0);

    FragColor = vec4(prefilteredColor, 1.0);
}

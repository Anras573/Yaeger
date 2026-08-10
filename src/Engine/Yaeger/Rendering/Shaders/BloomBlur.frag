#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uSource;
uniform vec2 uTexelSize; // 1 / (width, height) of uSource, in texels
uniform int uHorizontal; // 1 = blur along X, 0 = blur along Y

// Normalised 9-tap Gaussian weights (centre + 4 taps either side).
const float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

void main() {
    vec2 direction = uHorizontal == 1 ? vec2(uTexelSize.x, 0.0) : vec2(0.0, uTexelSize.y);
    vec3 result = texture(uSource, vTexCoord).rgb * weights[0];

    for (int i = 1; i < 5; i++) {
        vec2 offset = direction * float(i);
        result += texture(uSource, vTexCoord + offset).rgb * weights[i];
        result += texture(uSource, vTexCoord - offset).rgb * weights[i];
    }

    FragColor = vec4(result, 1.0);
}

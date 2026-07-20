#version 330 core
in  vec3 vLocalPos;
out vec4 FragColor;

uniform samplerCube uSource;

const float PI = 3.14159265359;

void main() {
    vec3 N = normalize(vLocalPos);
    vec3 up = abs(N.y) < 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 right = normalize(cross(up, N));
    up = normalize(cross(N, right));

    vec3 irradiance = vec3(0.0);
    float sampleDelta = 0.05;
    float sampleCount = 0.0;
    for (float phi = 0.0; phi < 2.0 * PI; phi += sampleDelta) {
        for (float theta = 0.0; theta < 0.5 * PI; theta += sampleDelta) {
            // Spherical -> tangent-space direction, then into world space around N.
            vec3 tangentSample = vec3(
                sin(theta) * cos(phi), sin(theta) * sin(phi), cos(theta)
            );
            vec3 sampleDir =
                tangentSample.x * right + tangentSample.y * up + tangentSample.z * N;

            vec3 radiance = pow(texture(uSource, sampleDir).rgb, vec3(2.2));
            irradiance += radiance * cos(theta) * sin(theta);
            sampleCount += 1.0;
        }
    }
    irradiance = PI * irradiance / sampleCount;

    FragColor = vec4(irradiance, 1.0);
}

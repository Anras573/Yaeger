#version 330 core
// Per-vertex: a unit quad (corners in [-0.5, 0.5]) shared by every particle draw.
layout(location = 0) in vec2 aCorner;
layout(location = 1) in vec2 aTexCoord;

// Per-instance (VertexAttribDivisor 1), populated by Renderer3D.DrawParticles.
layout(location = 2) in vec3 aInstancePosition;
layout(location = 3) in vec2 aInstanceSize;
layout(location = 4) in float aInstanceRotation;
layout(location = 5) in vec4 aInstanceColor;
// (uMin, vMin, uMax, vMax) of this particle's current flipbook frame - BillboardMath.GetFrameUv.
layout(location = 6) in vec4 aInstanceUvRect;

uniform mat4 uViewProj;
// World-space camera right/up axes (BillboardMath.ExtractCameraAxes) - every particle's quad is
// built from these two vectors so it always faces the camera, however the camera is oriented.
uniform vec3 uCameraRight;
uniform vec3 uCameraUp;

out vec2 vTexCoord;
out vec4 vColor;

void main() {
    float c = cos(aInstanceRotation);
    float s = sin(aInstanceRotation);
    vec2 scaledCorner = aCorner * aInstanceSize;
    // Rotate the quad-local corner within the camera's (right, up) plane so a velocity-stretched
    // billboard's long axis aligns with its projected direction of travel (BillboardMath.ProjectVelocity).
    vec2 rotatedCorner = vec2(
        scaledCorner.x * c - scaledCorner.y * s,
        scaledCorner.x * s + scaledCorner.y * c
    );

    vec3 worldPos = aInstancePosition
        + uCameraRight * rotatedCorner.x
        + uCameraUp * rotatedCorner.y;

    // Map the quad-local [0,1] texcoord into this particle's flipbook frame sub-rect.
    vTexCoord = mix(aInstanceUvRect.xy, aInstanceUvRect.zw, aTexCoord);
    vColor = aInstanceColor;
    gl_Position = uViewProj * vec4(worldPos, 1.0);
}

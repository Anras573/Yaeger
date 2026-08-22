#version 330 core
in vec3 vWorldPos;

uniform vec3  uLightPos;
uniform float uFarPlane;

void main() {
    // Store linear distance from the light, normalized by the far plane, instead of the default
    // non-linear perspective depth - the standard point-shadow technique (distance is continuous
    // across a cube's face boundaries, so sampling near a corner/edge in the lighting pass never
    // reads a discontinuous depth from the "wrong" face, which is what avoids seams/light leaks
    // there). uFarPlane is the casting light's own Range: distance is never sampled beyond it.
    gl_FragDepth = clamp(length(vWorldPos - uLightPos) / uFarPlane, 0.0, 1.0);
}

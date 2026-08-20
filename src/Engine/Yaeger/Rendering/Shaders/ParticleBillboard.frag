#version 330 core
in vec2 vTexCoord;
in vec4 vColor;
out vec4 FragColor;

// Unlit: particles are their own light source (glows, sparks, embers) or read as flat colour
// (smoke, dust), so no scene lighting is sampled here — matches how UiRenderer/SkyboxRenderer
// stay outside Renderer3D's lit main pass.
uniform sampler2D uTexture;

// Soft particles: fades alpha out as this fragment nears the opaque scene surface behind it,
// hiding the hard straight-line edge a billboard's depth test alone produces where it intersects
// geometry. See BillboardMath.LinearizeDepth/FadeFactor for the CPU-testable mirror of this math —
// Renderer3D.SetSceneDepth uploads uSceneDepth/uNear/uFar/uInvViewportSize once per frame,
// DrawParticles uploads uSoftFadeDistance per emitter.
uniform sampler2D uSceneDepth;
uniform int uSoftFadeSceneAvailable; // 0 until Renderer3D.SetSceneDepth is called
uniform float uSoftFadeDistance; // this emitter's ParticleEmitter3D.SoftFade; 0 = hard cutoff
uniform float uNear;
uniform float uFar;
uniform vec2 uInvViewportSize;

// Converts a non-linear perspective device depth in [0, 1] to linear view-space depth (positive
// distance from the camera, in world units) — mirrors BillboardMath.LinearizeDepth exactly.
float linearizeDepth(float deviceDepth) {
    float ndc = deviceDepth * 2.0 - 1.0;
    float denom = uFar + uNear - ndc * (uFar - uNear);
    return abs(denom) > 1e-8 ? (2.0 * uNear * uFar) / denom : uFar;
}

void main() {
    vec4 color = texture(uTexture, vTexCoord) * vColor;

    if (uSoftFadeSceneAvailable != 0 && uSoftFadeDistance > 0.0) {
        vec2 screenUv = gl_FragCoord.xy * uInvViewportSize;
        float sceneDepth = linearizeDepth(texture(uSceneDepth, screenUv).r);
        float particleDepth = linearizeDepth(gl_FragCoord.z);
        float fade = clamp((sceneDepth - particleDepth) / uSoftFadeDistance, 0.0, 1.0);
        color.a *= fade;
    }

    FragColor = color;
}

#version 330 core
in  vec3 vNormal;
in  vec2 vTexCoord;
in  vec3 vFragPos;
in  vec3 vTangent;
in  vec4 vLightSpacePos;
out vec4 FragColor;

uniform sampler2D uDiffuse;
uniform sampler2D uNormalMap;
uniform sampler2D uMetallicRoughnessMap;
uniform sampler2D uAoMap;
uniform sampler2D uEmissiveMap;
uniform sampler2D uShadowMap;
uniform int       uHasNormalMap;
uniform int       uHasMetallicRoughnessMap;
uniform int       uHasAoMap;
uniform int       uHasEmissiveMap;

uniform int   uShadowsEnabled;
uniform float uShadowBias;
uniform int   uUsePcf;

// Scales how far the shadow darkens, in [0, 1]: 1 is a full-strength shadow, 0 leaves the fragment
// fully lit. Lets a setting sun fade its shadows out instead of switching them off in one frame.
uniform float uShadowStrength;

uniform vec4  uDiffuseColor;
uniform vec4  uAmbientColor;
uniform vec4  uSpecularColor;
uniform float uShininess;

// Blend mode: 0 = Opaque, 1 = Cutout (alpha test via discard), 2 = Transparent, 3 = Additive
// (Transparent and Additive both render in the same depth-write-off pass on the CPU side; only the
// glBlendFunc differs between them, selected per-draw by Renderer3D.ApplyBlendFunc - the shader
// itself computes the same alpha-weighted colour either way). uOpacity is an extra alpha factor
// independent of any texture's own alpha channel; uAlphaCutoff only matters for Cutout.
uniform float uOpacity;
uniform int   uBlendMode;
uniform float uAlphaCutoff;

uniform int   uUsePbr;
uniform float uMetallicFactor;
uniform float uRoughnessFactor;
uniform vec4  uEmissiveColor;
uniform float uEmissiveIntensity;

// 0 = write LDR: Reinhard tone-map + gamma-encode to sRGB in-shader (the original behaviour,
// correct when this pass targets the backbuffer or an LDR RenderTarget directly).
// 1 = write linear HDR colour unclamped, deferring tone-mapping/gamma-encoding to a
// ToneMapEffect later in a PostProcessStack's HDR chain. See Renderer3D's constructor remarks.
uniform int   uHdrOutput;

// Distance fog: mixes fragment colour toward uFogColor as camera distance grows. Applied after
// lighting/emissive/ambient, before the alpha write, identically in both shading paths - see
// Renderer3D.SetFog. uFogMode: 0 = exponential-squared (uFogDensity), 1 = linear (uFogStart/uFogEnd).
uniform int   uFogEnabled;
uniform vec4  uFogColor;
uniform int   uFogMode;
uniform float uFogDensity;
uniform float uFogStart;
uniform float uFogEnd;

// Directional lights. Two slots so a day/night cycle can light dawn and dusk with a sun and a
// moon at once; a scene with one light leaves the second slot unused (uDirLightCount == 1).
#define MAX_DIR_LIGHTS 2

struct DirLight {
    vec3  direction;  // toward the light, normalised on upload
    vec4  color;
    float intensity;
};

uniform DirLight uDirLights[MAX_DIR_LIGHTS];
uniform int      uDirLightCount;

// Index of the light the shadow map was rendered from, or -1 when nothing casts. Only that light's
// contribution is shadowed: there is one map, so a second caster would darken with the wrong depths.
uniform int      uShadowLightIndex;

uniform vec3  uCameraPos;

// Scene-wide ambient for the PBR path, pre-multiplied by its intensity on the CPU side (see
// Renderer3D.SetAmbient). Defaults to vec3(0.03) - the constant this replaced - and is unused
// while uUseIBL is set, since image-based lighting supplies a directional ambient instead.
uniform vec3  uAmbientLight;

uniform samplerCube uIrradianceMap;
uniform samplerCube uPrefilteredMap;
uniform sampler2D   uBrdfLut;
uniform int         uUseIBL;
uniform float       uMaxReflectionLod;

#define MAX_POINT_LIGHTS 16
#define MAX_SPOT_LIGHTS 8

struct PointLight {
    vec3  position;
    vec4  color;
    float intensity;
    float range;
};

struct SpotLight {
    vec3  position;
    vec3  direction;  // beam axis, from the light outward (normalised)
    vec4  color;
    float intensity;
    float innerCos;   // cos(innerConeAngle); fully lit at or below this angle
    float outerCos;   // cos(outerConeAngle); fully dark beyond this angle
    float range;
};

uniform int        uPointLightCount;
uniform PointLight uPointLights[MAX_POINT_LIGHTS];
uniform int        uSpotLightCount;
uniform SpotLight  uSpotLights[MAX_SPOT_LIGHTS];

const float PI = 3.14159265359;

// Smooth, range-based distance attenuation (UE4-style): an inverse-square falloff windowed
// so the contribution reaches exactly zero at `range`, avoiding a hard cutoff edge.
float attenuate(float dist, float range) {
    if (range <= 0.0) return 0.0;
    float ratio = dist / range;
    float window = clamp(1.0 - ratio * ratio * ratio * ratio, 0.0, 1.0);
    return (window * window) / (dist * dist + 1.0);
}

// Cone falloff for a spot light. `L` points from the fragment toward the light. Equivalent
// to smoothstep(outerCos, innerCos, cosAngle) but guards the edge0==edge1 case (a zero-width
// cone edge) that would otherwise divide by zero.
float spotFactor(vec3 L, vec3 spotDir, float innerCos, float outerCos) {
    float cosAngle = dot(-L, spotDir);
    float t = clamp((cosAngle - outerCos) / max(innerCos - outerCos, 1e-4), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

// Fog visibility in [0, 1] at the given camera distance: 1 = no fog, 0 = fully fog-coloured.
float fogFactor(float dist) {
    if (uFogMode == 1) {
        // Linear: SetFog guarantees uFogEnd > uFogStart, so this division never degenerates.
        return 1.0 - clamp((dist - uFogStart) / (uFogEnd - uFogStart), 0.0, 1.0);
    }
    // Exponential-squared: no hard edge, thickens gradually with distance.
    float d = dist * uFogDensity;
    return clamp(exp(-(d * d)), 0.0, 1.0);
}

float distributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    denom = PI * denom * denom;
    return a2 / max(denom, 1e-7);
}

float geometrySchlickGGX(float NdotX, float roughness) {
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotX / (NdotX * (1.0 - k) + k);
}

float geometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return geometrySchlickGGX(NdotV, roughness) * geometrySchlickGGX(NdotL, roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// Roughness-aware Fresnel (Sebastien Lagarde) for ambient/IBL use: widens the
// grazing-angle reflectance term so rough surfaces don't show an unnaturally sharp
// Fresnel rim the way the direct-light fresnelSchlick would.
vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness) {
    vec3 maxReflectance = max(vec3(1.0 - roughness), F0);
    return F0 + (maxReflectance - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// Cook-Torrance contribution of a single light. `L` points from the fragment toward the
// light; `radiance` already folds in the light's colour, intensity and any attenuation.
vec3 pbrContribution(
    vec3 N, vec3 V, vec3 L, vec3 radiance,
    vec3 albedo, float metallic, float roughness, vec3 F0
) {
    vec3 halfDir = L + V;
    vec3 H = halfDir * inversesqrt(max(dot(halfDir, halfDir), 1e-10));

    float NDF = distributionGGX(N, H, roughness);
    float G   = geometrySmith(N, V, L, roughness);
    vec3  F   = fresnelSchlick(max(dot(H, V), 0.0), F0);

    float NdotL = max(dot(N, L), 0.0);
    vec3  numerator = NDF * G * F;
    float denom = 4.0 * max(dot(N, V), 0.0) * NdotL + 1e-4;
    vec3  specular = numerator / denom;

    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    return (kD * albedo / PI + specular) * radiance * NdotL;
}

// Blinn-Phong contribution of a single light. `radiance` already folds in the light's
// colour, intensity and any attenuation.
vec3 phongContribution(
    vec3 N, vec3 V, vec3 L, vec3 radiance,
    vec3 texColor, vec3 specColor, float shininess
) {
    vec3 halfDir = L + V;
    vec3 H = halfDir * inversesqrt(max(dot(halfDir, halfDir), 1e-10));
    float diff = max(dot(N, L), 0.0);
    float spec = diff > 0.0 ? pow(max(dot(N, H), 0.0), shininess) : 0.0;
    return (texColor * diff + specColor * spec) * radiance;
}

// Directional-light visibility in [0, 1]: 1 = fully lit, 0 = fully shadowed. Projects the
// fragment into light space, compares its depth against the shadow map, and (optionally)
// averages a 3x3 PCF kernel for soft edges. Only the directional light casts shadows in v1.
float directionalShadow(vec3 N, vec3 L) {
    if (uShadowsEnabled == 0) return 1.0;

    // Back-facing to the light: both shading paths clamp the directional term to zero, so
    // the shadow factor is irrelevant (shadow * 0 == 0). Skip the (PCF) texture reads.
    if (dot(N, L) <= 0.0) return 1.0;

    // Perspective divide, then map NDC -> [0, 1] texture/depth space.
    vec3 proj = vLightSpacePos.xyz / vLightSpacePos.w;
    proj = proj * 0.5 + 0.5;

    // Outside the light's depth range (in front of its near plane or beyond the far
    // plane) or outside the map footprint: treat as lit.
    if (proj.z < 0.0 || proj.z > 1.0) return 1.0;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0) return 1.0;

    // Slope-scaled bias: grazing angles need more offset to avoid shadow acne.
    float bias = max(uShadowBias * (1.0 - dot(N, L)), uShadowBias * 0.1);
    float current = proj.z;

    // mix(1.0, factor, strength): a strength of 0 leaves every fragment lit, 1 is the full shadow.
    float strength = clamp(uShadowStrength, 0.0, 1.0);

    if (uUsePcf != 0) {
        float sum = 0.0;
        vec2 texel = 1.0 / vec2(textureSize(uShadowMap, 0));
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                float closest = texture(uShadowMap, proj.xy + vec2(x, y) * texel).r;
                sum += current - bias > closest ? 1.0 : 0.0;
            }
        }
        return mix(1.0, 1.0 - sum / 9.0, strength);
    }

    float closest = texture(uShadowMap, proj.xy).r;
    return mix(1.0, current - bias > closest ? 0.0 : 1.0, strength);
}

void main() {
    vec3 N = normalize(vNormal);

    if (uHasNormalMap != 0) {
        float tLenSq = dot(vTangent, vTangent);
        if (tLenSq > 1e-10) {
            vec3 T = vTangent * inversesqrt(tLenSq);
            vec3 Tproj = T - dot(T, N) * N;
            float projLenSq = dot(Tproj, Tproj);
            if (projLenSq > 1e-10) {
                vec3 Tn = Tproj * inversesqrt(projLenSq);
                vec3 B = cross(N, Tn);
                mat3 TBN = mat3(Tn, B, N);
                vec3 sampledN = texture(uNormalMap, vTexCoord).rgb * 2.0 - 1.0;
                N = normalize(TBN * sampledN);
            }
        }
    }

    vec3 viewDir = uCameraPos - vFragPos;
    vec3 V = viewDir * inversesqrt(max(dot(viewDir, viewDir), 1e-10));
    float fragDist = length(viewDir);

    vec4 rawTex = texture(uDiffuse, vTexCoord);

    if (uUsePbr != 0) {
        // glTF base colour texture is sRGB-encoded; linearise it before applying the
        // base-colour factor, which glTF defines in linear space.
        vec3 albedo = pow(rawTex.rgb, vec3(2.2)) * uDiffuseColor.rgb;

        float metallic  = uMetallicFactor;
        float roughness = uRoughnessFactor;
        if (uHasMetallicRoughnessMap != 0) {
            // glTF packs roughness in G and metallic in B.
            vec3 mr = texture(uMetallicRoughnessMap, vTexCoord).rgb;
            roughness *= mr.g;
            metallic  *= mr.b;
        }
        roughness = clamp(roughness, 0.04, 1.0);
        metallic  = clamp(metallic, 0.0, 1.0);

        float ao = uHasAoMap != 0 ? texture(uAoMap, vTexCoord).r : 1.0;

        vec3 emissive = uEmissiveColor.rgb * uEmissiveIntensity;
        if (uHasEmissiveMap != 0)
            emissive *= pow(texture(uEmissiveMap, vTexCoord).rgb, vec3(2.2));

        vec3 F0 = mix(vec3(0.04), albedo, metallic);

        // Directional lights. Only the slot the shadow map belongs to is shadowed (1 = lit).
        vec3 Lo = vec3(0.0);
        for (int i = 0; i < uDirLightCount; i++) {
            vec3 L = normalize(uDirLights[i].direction);
            float shadow = i == uShadowLightIndex ? directionalShadow(N, L) : 1.0;
            Lo += pbrContribution(
                N, V, L, uDirLights[i].color.rgb * uDirLights[i].intensity,
                albedo, metallic, roughness, F0
            ) * shadow;
        }

        // Point lights.
        for (int i = 0; i < uPointLightCount; i++) {
            vec3 toLight = uPointLights[i].position - vFragPos;
            float dist = length(toLight);
            vec3 Lp = toLight * inversesqrt(max(dot(toLight, toLight), 1e-10));
            float att = attenuate(dist, uPointLights[i].range);
            vec3 radiance = uPointLights[i].color.rgb * uPointLights[i].intensity * att;
            Lo += pbrContribution(N, V, Lp, radiance, albedo, metallic, roughness, F0);
        }

        // Spot lights.
        for (int i = 0; i < uSpotLightCount; i++) {
            vec3 toLight = uSpotLights[i].position - vFragPos;
            float dist = length(toLight);
            vec3 Ls = toLight * inversesqrt(max(dot(toLight, toLight), 1e-10));
            float att = attenuate(dist, uSpotLights[i].range);
            float spot = spotFactor(
                Ls, uSpotLights[i].direction,
                uSpotLights[i].innerCos, uSpotLights[i].outerCos
            );
            vec3 radiance = uSpotLights[i].color.rgb * uSpotLights[i].intensity * att * spot;
            Lo += pbrContribution(N, V, Ls, radiance, albedo, metallic, roughness, F0);
        }

        vec3 ambient;
        if (uUseIBL != 0) {
            // Split-sum image-based lighting (Karis, "Real Shading in Unreal Engine 4"):
            // uIrradianceMap/uPrefilteredMap/uBrdfLut are pre-baked by IblPrefilter and
            // already linear, so no further colour-space conversion is needed here.
            vec3 Fr = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
            vec3 kD = (vec3(1.0) - Fr) * (1.0 - metallic);

            vec3 irradiance = texture(uIrradianceMap, N).rgb;
            vec3 diffuseIBL = irradiance * albedo;

            vec3 R = reflect(-V, N);
            vec3 prefilteredColor =
                textureLod(uPrefilteredMap, R, roughness * uMaxReflectionLod).rgb;
            vec2 envBRDF = texture(uBrdfLut, vec2(max(dot(N, V), 0.0), roughness)).rg;
            vec3 specularIBL = prefilteredColor * (Fr * envBRDF.x + envBRDF.y);

            ambient = (kD * diffuseIBL + specularIBL) * ao;
        } else {
            // Flat ambient fallback for scenes without a skybox. uAmbientLight defaults to the
            // vec3(0.03) this used to hardcode, so an untouched scene is unchanged.
            ambient = uAmbientLight * albedo * ao;
        }
        vec3 color = ambient + Lo + emissive;

        // Fog is mixed in before tone-mapping: with uHdrOutput == 1 that compression is deferred to
        // a later ToneMapEffect pass, which must see the fogged colour, not a pre-fog one.
        if (uFogEnabled != 0) {
            color = mix(uFogColor.rgb, color, fogFactor(fragDist));
        }

        if (uHdrOutput == 0) {
            // Reinhard tone-map, then gamma encode back to sRGB.
            color = color / (color + vec3(1.0));
            color = pow(color, vec3(1.0 / 2.2));
        }
        // else: leave color as linear HDR (may exceed 1.0) for a ToneMapEffect to compress later.

        FragColor = vec4(color, rawTex.a * uDiffuseColor.a * uOpacity);
    } else {
        vec4 texColor = rawTex * uDiffuseColor;

        // Directional lights, shadowed the same way as the PBR path above.
        vec3 lit = vec3(0.0);
        for (int i = 0; i < uDirLightCount; i++) {
            vec3 L = normalize(uDirLights[i].direction);
            float shadow = i == uShadowLightIndex ? directionalShadow(N, L) : 1.0;
            lit += phongContribution(
                N, V, L, uDirLights[i].color.rgb * uDirLights[i].intensity,
                texColor.rgb, uSpecularColor.rgb, uShininess
            ) * shadow;
        }

        // Point lights.
        for (int i = 0; i < uPointLightCount; i++) {
            vec3 toLight = uPointLights[i].position - vFragPos;
            float dist = length(toLight);
            vec3 Lp = toLight * inversesqrt(max(dot(toLight, toLight), 1e-10));
            float att = attenuate(dist, uPointLights[i].range);
            vec3 radiance = uPointLights[i].color.rgb * uPointLights[i].intensity * att;
            lit += phongContribution(
                N, V, Lp, radiance, texColor.rgb, uSpecularColor.rgb, uShininess
            );
        }

        // Spot lights.
        for (int i = 0; i < uSpotLightCount; i++) {
            vec3 toLight = uSpotLights[i].position - vFragPos;
            float dist = length(toLight);
            vec3 Ls = toLight * inversesqrt(max(dot(toLight, toLight), 1e-10));
            float att = attenuate(dist, uSpotLights[i].range);
            float spot = spotFactor(
                Ls, uSpotLights[i].direction,
                uSpotLights[i].innerCos, uSpotLights[i].outerCos
            );
            vec3 radiance = uSpotLights[i].color.rgb * uSpotLights[i].intensity * att * spot;
            lit += phongContribution(
                N, V, Ls, radiance, texColor.rgb, uSpecularColor.rgb, uShininess
            );
        }

        vec3 ambient = (uAmbientColor * rawTex).rgb;
        vec3 color = ambient + lit;

        if (uFogEnabled != 0) {
            color = mix(uFogColor.rgb, color, fogFactor(fragDist));
        }

        FragColor = vec4(color, texColor.a * uOpacity);
    }

    // Cutout alpha test: discard fully-transparent-enough fragments instead of blending them, so
    // the main (depth-write-on) pass can render foliage/fences with no sorting required. Opaque
    // and Transparent materials never discard here.
    if (uBlendMode == 1 && FragColor.a < uAlphaCutoff) {
        discard;
    }
}

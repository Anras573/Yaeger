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

uniform vec4  uDiffuseColor;
uniform vec4  uAmbientColor;
uniform vec4  uSpecularColor;
uniform float uShininess;

uniform int   uUsePbr;
uniform float uMetallicFactor;
uniform float uRoughnessFactor;
uniform vec4  uEmissiveColor;

uniform vec3  uLightDir;
uniform vec4  uLightColor;
uniform float uLightIntensity;
uniform vec3  uCameraPos;

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

// Roughness-aware Fresnel (Sébastien Lagarde) for ambient/IBL use: widens the
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

    if (uUsePcf != 0) {
        float sum = 0.0;
        vec2 texel = 1.0 / vec2(textureSize(uShadowMap, 0));
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                float closest = texture(uShadowMap, proj.xy + vec2(x, y) * texel).r;
                sum += current - bias > closest ? 1.0 : 0.0;
            }
        }
        return 1.0 - sum / 9.0;
    }

    float closest = texture(uShadowMap, proj.xy).r;
    return current - bias > closest ? 0.0 : 1.0;
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

    vec3 L = normalize(uLightDir);
    vec3 viewDir = uCameraPos - vFragPos;
    vec3 V = viewDir * inversesqrt(max(dot(viewDir, viewDir), 1e-10));

    // Directional shadowing (1 = lit, 0 = shadowed); only the directional light casts.
    float shadow = directionalShadow(N, L);

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

        vec3 emissive = uEmissiveColor.rgb;
        if (uHasEmissiveMap != 0)
            emissive *= pow(texture(uEmissiveMap, vTexCoord).rgb, vec3(2.2));

        vec3 F0 = mix(vec3(0.04), albedo, metallic);

        // Directional light (shadowed).
        vec3 Lo = pbrContribution(
            N, V, L, uLightColor.rgb * uLightIntensity,
            albedo, metallic, roughness, F0
        ) * shadow;

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
            // Flat ambient fallback for scenes without a skybox — unchanged from the
            // pre-IBL behaviour.
            ambient = vec3(0.03) * albedo * ao;
        }
        vec3 color = ambient + Lo + emissive;

        // Reinhard tone-map, then gamma encode back to sRGB.
        color = color / (color + vec3(1.0));
        color = pow(color, vec3(1.0 / 2.2));

        FragColor = vec4(color, rawTex.a * uDiffuseColor.a);
    } else {
        vec4 texColor = rawTex * uDiffuseColor;

        // Directional light (shadowed).
        vec3 lit = phongContribution(
            N, V, L, uLightColor.rgb * uLightIntensity,
            texColor.rgb, uSpecularColor.rgb, uShininess
        ) * shadow;

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

        FragColor = vec4(ambient + lit, texColor.a);
    }
}

using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Renders 3D meshes with MVP transforms, depth testing, and back-face culling.
/// Independent of the 2D <see cref="Renderer"/> pipeline.
/// </summary>
public sealed class Renderer3D : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;
        layout(location = 3) in vec3 aTangent;
        layout(location = 4) in vec4 aBoneIndices;
        layout(location = 5) in vec4 aBoneWeights;

        uniform mat4 uModel;
        uniform mat4 uViewProj;
        uniform mat3 uNormalMatrix;
        uniform mat4 uLightSpaceMatrix;

        // GPU skinning: a palette of bone matrices supplied via a uniform buffer. uSkinned gates the
        // whole path so static meshes (all weights zero) are unaffected.
        const int MAX_BONES = 128;
        layout(std140) uniform Bones {
            mat4 uBones[MAX_BONES];
        };
        uniform int uSkinned;

        out vec3 vNormal;
        out vec2 vTexCoord;
        out vec3 vFragPos;
        out vec3 vTangent;
        out vec4 vLightSpacePos;

        void main() {
            mat4 skin = mat4(1.0);
            if (uSkinned != 0) {
                float wSum = dot(aBoneWeights, vec4(1.0));
                // Guard against out-of-range indices (e.g. a model with more bones than the palette
                // holds): an OOB uBones[] read is undefined behaviour. Fall back to identity skin
                // (bind pose) when any of the four indices is outside [0, MAX_BONES).
                bool inRange =
                    all(greaterThanEqual(aBoneIndices, vec4(0.0))) &&
                    all(lessThan(aBoneIndices, vec4(float(MAX_BONES))));
                if (wSum > 1e-4 && inRange) {
                    skin =
                        uBones[int(aBoneIndices.x)] * aBoneWeights.x +
                        uBones[int(aBoneIndices.y)] * aBoneWeights.y +
                        uBones[int(aBoneIndices.z)] * aBoneWeights.z +
                        uBones[int(aBoneIndices.w)] * aBoneWeights.w;
                }
            }

            vec4 skinnedPos = skin * vec4(aPosition, 1.0);
            mat3 skin3 = mat3(skin);
            // Normals need the inverse-transpose of the skin matrix so non-uniform bone scale doesn't
            // skew them; tangents are surface directions and use the skin matrix directly. Both are
            // identity for static meshes (skin == identity).
            mat3 skinNormal = transpose(inverse(skin3));

            vec4 worldPos = uModel * skinnedPos;
            vFragPos  = worldPos.xyz;
            vNormal   = uNormalMatrix * (skinNormal * aNormal);
            vTangent  = mat3(uModel) * (skin3 * aTangent);
            vTexCoord = aTexCoord;
            vLightSpacePos = uLightSpaceMatrix * worldPos;
            gl_Position = uViewProj * worldPos;
        }
        """;

    private const string FragmentShaderSource = """
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

                vec3 ambient = vec3(0.03) * albedo * ao;
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
        """;

    /// <summary>Maximum number of bones the vertex shader's skinning palette can hold (matches MAX_BONES in GLSL).</summary>
    public const int MaxBones = 128;

    // Binding point linking the "Bones" uniform block to the bone-matrix UBO. Arbitrary but must not
    // collide with any other uniform block binding (the renderer has none).
    private const uint BoneBlockBinding = 0;

    /// <summary>Maximum number of point lights the fragment shader can accumulate per frame.</summary>
    public const int MaxPointLights = 16;

    /// <summary>Maximum number of spot lights the fragment shader can accumulate per frame.</summary>
    public const int MaxSpotLights = 8;

    // Per-light uniform names depend only on the array index, so build them once and reuse them
    // every frame. Interpolating them inside the per-frame upload loops would allocate a fresh
    // string per light field on every call.
    private static readonly string[] PointPositionNames;
    private static readonly string[] PointColorNames;
    private static readonly string[] PointIntensityNames;
    private static readonly string[] PointRangeNames;
    private static readonly string[] SpotPositionNames;
    private static readonly string[] SpotDirectionNames;
    private static readonly string[] SpotColorNames;
    private static readonly string[] SpotIntensityNames;
    private static readonly string[] SpotInnerCosNames;
    private static readonly string[] SpotOuterCosNames;
    private static readonly string[] SpotRangeNames;

    static Renderer3D()
    {
        PointPositionNames = BuildNames("uPointLights", "position", MaxPointLights);
        PointColorNames = BuildNames("uPointLights", "color", MaxPointLights);
        PointIntensityNames = BuildNames("uPointLights", "intensity", MaxPointLights);
        PointRangeNames = BuildNames("uPointLights", "range", MaxPointLights);
        SpotPositionNames = BuildNames("uSpotLights", "position", MaxSpotLights);
        SpotDirectionNames = BuildNames("uSpotLights", "direction", MaxSpotLights);
        SpotColorNames = BuildNames("uSpotLights", "color", MaxSpotLights);
        SpotIntensityNames = BuildNames("uSpotLights", "intensity", MaxSpotLights);
        SpotInnerCosNames = BuildNames("uSpotLights", "innerCos", MaxSpotLights);
        SpotOuterCosNames = BuildNames("uSpotLights", "outerCos", MaxSpotLights);
        SpotRangeNames = BuildNames("uSpotLights", "range", MaxSpotLights);
    }

    private static string[] BuildNames(string array, string field, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{array}[{i}].{field}";
        return names;
    }

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _defaultTexture;
    private readonly uint _defaultNormalTexture;
    private readonly uint _boneUbo;

    public Renderer3D(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _defaultTexture = CreateWhiteTexture();
        _defaultNormalTexture = CreateFlatNormalTexture();
        _boneUbo = CreateBoneUbo();
        BindSamplerUnits();
        BindDefaultPbrTextures();
        // DisableShadows also binds the default texture on unit 5, so no separate setup is needed.
        DisableShadows();
        // Skinning is opt-in per draw; default to the static-mesh path.
        _shader.Bind();
        _shader.SetUniformInt("uSkinned", 0);
        _shader.Unbind();
        SetSceneLighting(DirectionalLight.Default, Vector3.Zero);
        // Start with no point/spot lights so scenes that never call SetPointLights/SetSpotLights
        // (the pre-existing single-directional-light path) render exactly as before.
        SetPointLights([]);
        SetSpotLights([]);
    }

    // Sampler-to-texture-unit assignments never change after link, so set them once here rather
    // than re-uploading them on every Draw call.
    private void BindSamplerUnits()
    {
        _shader.Bind();
        _shader.SetUniformInt("uDiffuse", 0);
        _shader.SetUniformInt("uNormalMap", 1);
        _shader.SetUniformInt("uMetallicRoughnessMap", 2);
        _shader.SetUniformInt("uAoMap", 3);
        _shader.SetUniformInt("uEmissiveMap", 4);
        _shader.SetUniformInt("uShadowMap", 5);
        _shader.Unbind();
    }

    // Bind the 1×1 white texture to the optional PBR sampler units (2-4) once at construction.
    // Those samplers are statically used by the fragment shader (the gating `uHas*Map` uniform
    // doesn't make them un-referenced), so each must point at a *complete* texture for defined
    // behaviour. Draw only ever overwrites these units with a real map and never unbinds them,
    // so this one-time bind keeps the units complete for the renderer's lifetime — Draw can then
    // skip binding a fallback when a map is absent.
    private void BindDefaultPbrTextures()
    {
        foreach (
            var unit in (ReadOnlySpan<TextureUnit>)
                [TextureUnit.Texture2, TextureUnit.Texture3, TextureUnit.Texture4]
        )
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        }

        // Restore the default active unit so we don't leak Texture4 into later GL setup (e.g. the
        // Texture constructor binds without first selecting a unit).
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    // The shadow sampler (unit 5) is statically used by the fragment shader, so it must point at a
    // complete texture even when shadows are disabled. Bind the 1×1 white texture (sampled as depth
    // 1.0 = fully lit) until SetShadowMap swaps in a real depth map. As with the PBR fallbacks, the
    // shadow path never unbinds this unit, so the one-time bind keeps it valid for the lifetime.
    private void BindDefaultShadowTexture()
    {
        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>
    /// Binds the shadow map and uploads the light-space transform for the lighting pass. Call once
    /// per frame, after the shadow pass has populated the depth texture and before the draw loop.
    /// </summary>
    public void SetShadowMap(
        Matrix4x4 lightSpaceMatrix,
        uint depthTexture,
        float bias,
        bool enablePcf
    )
    {
        _shader.Bind();
        _shader.SetUniformMatrix4("uLightSpaceMatrix", lightSpaceMatrix);
        _shader.SetUniformFloat("uShadowBias", SanitizeNonNegative(bias));
        _shader.SetUniformInt("uUsePcf", enablePcf ? 1 : 0);
        _shader.SetUniformInt("uShadowsEnabled", 1);

        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, depthTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _shader.Unbind();
    }

    /// <summary>
    /// Disables shadow sampling: the lighting pass treats every fragment as fully lit. This is the
    /// default state until <see cref="SetShadowMap"/> is called.
    /// </summary>
    public void DisableShadows()
    {
        _shader.Bind();
        _shader.SetUniformInt("uShadowsEnabled", 0);
        _shader.SetUniformMatrix4("uLightSpaceMatrix", Matrix4x4.Identity);
        _shader.Unbind();

        // Restore the default (complete) shadow texture on unit 5. After a prior SetShadowMap the
        // unit may still point at a depth texture that gets deleted when its ShadowMapRenderer is
        // disposed, leaving the statically-used sampler incomplete even though sampling is gated off.
        BindDefaultShadowTexture();
    }

    /// <summary>
    /// Enables depth testing and back-face culling, and clears the colour and depth buffers.
    /// Call once at the start of the 3D pass each frame.
    /// </summary>
    public void BeginFrame3D()
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    /// <summary>
    /// Disables depth testing and back-face culling so the 2D pipeline is unaffected.
    /// Call once at the end of the 3D pass each frame.
    /// </summary>
    public void EndFrame3D()
    {
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
    }

    /// <summary>
    /// Uploads scene-wide lighting uniforms. Call once per frame before the draw loop.
    /// </summary>
    public void SetSceneLighting(DirectionalLight light, Vector3 cameraPos)
    {
        var lenSq = light.Direction.LengthSquared();
        var dir =
            float.IsFinite(lenSq) && lenSq > 0f
                ? Vector3.Normalize(light.Direction)
                : Vector3.UnitY;
        var intensity = float.IsFinite(light.Intensity) ? MathF.Max(light.Intensity, 0f) : 0f;
        _shader.Bind();
        _shader.SetUniformVec3("uLightDir", dir);
        _shader.SetUniformVec4("uLightColor", light.Color.ToVector4());
        _shader.SetUniformFloat("uLightIntensity", intensity);
        _shader.SetUniformVec3("uCameraPos", cameraPos);
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the active point lights for this frame. Call once per frame before the draw loop.
    /// At most <see cref="MaxPointLights"/> lights are used; any extras are ignored. Passing an
    /// empty span disables all point lights.
    /// </summary>
    public void SetPointLights(ReadOnlySpan<(Vector3 Position, PointLight Light)> lights)
    {
        var count = Math.Min(lights.Length, MaxPointLights);
        _shader.Bind();
        _shader.SetUniformInt("uPointLightCount", count);
        for (var i = 0; i < count; i++)
        {
            var (position, light) = lights[i];
            _shader.SetUniformVec3(PointPositionNames[i], position);
            _shader.SetUniformVec4(PointColorNames[i], light.Color.ToVector4());
            _shader.SetUniformFloat(PointIntensityNames[i], SanitizeNonNegative(light.Intensity));
            _shader.SetUniformFloat(PointRangeNames[i], SanitizeNonNegative(light.Range));
        }
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the active spot lights for this frame. Call once per frame before the draw loop.
    /// At most <see cref="MaxSpotLights"/> lights are used; any extras are ignored. Passing an
    /// empty span disables all spot lights.
    /// </summary>
    public void SetSpotLights(ReadOnlySpan<(Vector3 Position, SpotLight Light)> lights)
    {
        var count = Math.Min(lights.Length, MaxSpotLights);
        _shader.Bind();
        _shader.SetUniformInt("uSpotLightCount", count);
        for (var i = 0; i < count; i++)
        {
            var (position, light) = lights[i];

            var lenSq = light.Direction.LengthSquared();
            var direction =
                float.IsFinite(lenSq) && lenSq > 0f
                    ? Vector3.Normalize(light.Direction)
                    : -Vector3.UnitY;

            // Clamp angles to [0, pi] and force inner <= outer so the cos values stay ordered
            // (innerCos >= outerCos), which smoothstep requires for a well-defined cone edge.
            var outerAngle = Math.Clamp(SanitizeNonNegative(light.OuterConeAngle), 0f, MathF.PI);
            var innerAngle = Math.Clamp(SanitizeNonNegative(light.InnerConeAngle), 0f, outerAngle);

            _shader.SetUniformVec3(SpotPositionNames[i], position);
            _shader.SetUniformVec3(SpotDirectionNames[i], direction);
            _shader.SetUniformVec4(SpotColorNames[i], light.Color.ToVector4());
            _shader.SetUniformFloat(SpotIntensityNames[i], SanitizeNonNegative(light.Intensity));
            _shader.SetUniformFloat(SpotInnerCosNames[i], MathF.Cos(innerAngle));
            _shader.SetUniformFloat(SpotOuterCosNames[i], MathF.Cos(outerAngle));
            _shader.SetUniformFloat(SpotRangeNames[i], SanitizeNonNegative(light.Range));
        }
        _shader.Unbind();
    }

    // Guards against NaN/negative values leaking into shader uniforms (mirrors SetSceneLighting).
    private static float SanitizeNonNegative(float value) =>
        float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;

    /// <summary>Draws a single static mesh with the supplied transform and material.</summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    ) => DrawCore(mesh, model, viewProj, material, textures, skinned: false);

    /// <summary>
    /// Draws a single skinned mesh, uploading <paramref name="bonePalette"/> to the bone-matrix UBO
    /// and enabling GPU skinning in the vertex shader. Up to <see cref="MaxBones"/> matrices are used.
    /// </summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures,
        ReadOnlySpan<Matrix4x4> bonePalette
    )
    {
        SetBoneMatrices(bonePalette);
        DrawCore(mesh, model, viewProj, material, textures, skinned: true);
    }

    private void DrawCore(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures,
        bool skinned
    )
    {
        _shader.Bind();

        _shader.SetUniformInt("uSkinned", skinned ? 1 : 0);

        _shader.SetUniformMatrix4("uModel", model);
        _shader.SetUniformMatrix4("uViewProj", viewProj);

        if (!Matrix4x4.Invert(model, out var invModel))
            invModel = Matrix4x4.Identity;
        _shader.SetUniformMatrix3("uNormalMatrix", Matrix4x4.Transpose(invModel));

        _shader.SetUniformVec4("uDiffuseColor", material.Diffuse.ToVector4());
        _shader.SetUniformVec4("uAmbientColor", material.Ambient.ToVector4());
        _shader.SetUniformVec4("uSpecularColor", material.Specular.ToVector4());
        _shader.SetUniformFloat(
            "uShininess",
            float.IsFinite(material.Shininess) ? MathF.Max(material.Shininess, 1f) : 1f
        );

        var metallic = float.IsFinite(material.MetallicFactor)
            ? Math.Clamp(material.MetallicFactor, 0f, 1f)
            : 1f;
        var roughness = float.IsFinite(material.RoughnessFactor)
            ? Math.Clamp(material.RoughnessFactor, 0f, 1f)
            : 1f;

        _shader.SetUniformInt("uUsePbr", material.UsePbr ? 1 : 0);
        _shader.SetUniformFloat("uMetallicFactor", metallic);
        _shader.SetUniformFloat("uRoughnessFactor", roughness);
        _shader.SetUniformVec4("uEmissiveColor", material.EmissiveColor.ToVector4());

        // Select the target unit *before* TextureManager.Get: a first-time Get constructs a
        // Texture whose ctor binds on the currently-active unit, which would otherwise clobber a
        // previously-bound unit. Activating first keeps that side-effect bind on the right unit.
        _gl.ActiveTexture(TextureUnit.Texture0);
        if (!string.IsNullOrEmpty(material.DiffuseTexturePath))
            textures.Get(material.DiffuseTexturePath).Bind(TextureUnit.Texture0);
        else
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        _gl.ActiveTexture(TextureUnit.Texture1);
        if (!string.IsNullOrEmpty(material.NormalTexturePath))
        {
            textures.Get(material.NormalTexturePath).Bind(TextureUnit.Texture1);
            _shader.SetUniformInt("uHasNormalMap", 1);
        }
        else
        {
            _gl.BindTexture(TextureTarget.Texture2D, _defaultNormalTexture);
            _shader.SetUniformInt("uHasNormalMap", 0);
        }

        // Only the PBR branch samples the metallic-roughness/AO/emissive maps, so skip the
        // texture binds entirely for Blinn-Phong materials and just clear the has-flags.
        if (material.UsePbr)
        {
            BindOptionalTexture(
                textures,
                material.MetallicRoughnessTexturePath,
                TextureUnit.Texture2,
                "uHasMetallicRoughnessMap"
            );
            BindOptionalTexture(
                textures,
                material.AoTexturePath,
                TextureUnit.Texture3,
                "uHasAoMap"
            );
            BindOptionalTexture(
                textures,
                material.EmissiveTexturePath,
                TextureUnit.Texture4,
                "uHasEmissiveMap"
            );
        }
        else
        {
            _shader.SetUniformInt("uHasMetallicRoughnessMap", 0);
            _shader.SetUniformInt("uHasAoMap", 0);
            _shader.SetUniformInt("uHasEmissiveMap", 0);
        }

        mesh.Draw();

        _shader.Unbind();
    }

    // Binds an optional PBR texture to the given unit and flags its presence. When the path is
    // empty the bind is skipped: the unit already holds a complete fallback texture from
    // construction (see BindDefaultPbrTextures), so the (uniform-gated) sampler stays valid and
    // `uHas*Map = 0` tells the shader to ignore it. Sampler-to-unit assignments are likewise set
    // once at construction (see BindSamplerUnits), so they aren't re-uploaded here.
    private void BindOptionalTexture(
        TextureManager textures,
        string? path,
        TextureUnit unit,
        string hasUniform
    )
    {
        if (!string.IsNullOrEmpty(path))
        {
            // Select the unit before Get so a first-time Texture ctor binds on this unit rather
            // than clobbering whichever unit happened to be active.
            _gl.ActiveTexture(unit);
            textures.Get(path).Bind(unit);
            _shader.SetUniformInt(hasUniform, 1);
        }
        else
        {
            _shader.SetUniformInt(hasUniform, 0);
        }
    }

    private unsafe uint CreateWhiteTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] white = [255, 255, 255, 255];
        fixed (byte* ptr = white)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                1,
                1,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest
        );
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    // Flat normal map: (0.5, 0.5, 1.0) encodes tangent-space normal pointing straight up.
    private unsafe uint CreateFlatNormalTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] flatNormal = [128, 128, 255, 255];
        fixed (byte* ptr = flatNormal)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                1,
                1,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest
        );
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    // Allocates the bone-matrix uniform buffer (MaxBones mat4s) and links it to the shader's "Bones"
    // block via a shared binding point. Filled per skinned draw by SetBoneMatrices.
    private unsafe uint CreateBoneUbo()
    {
        var ubo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, ubo);
        _gl.BufferData(
            BufferTargetARB.UniformBuffer,
            (nuint)(MaxBones * sizeof(Matrix4x4)),
            null,
            BufferUsageARB.DynamicDraw
        );
        _gl.BindBufferBase(BufferTargetARB.UniformBuffer, BoneBlockBinding, ubo);
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
        _shader.BindUniformBlock("Bones", BoneBlockBinding);
        return ubo;
    }

    /// <summary>
    /// Uploads a skinning matrix palette to the bone UBO. At most <see cref="MaxBones"/> matrices are
    /// used; extras are ignored. The skinned <see cref="Draw(GpuMesh, Matrix4x4, Matrix4x4, Material3D, TextureManager, ReadOnlySpan{Matrix4x4})"/>
    /// overload calls this for you.
    /// </summary>
    public unsafe void SetBoneMatrices(ReadOnlySpan<Matrix4x4> palette)
    {
        var count = Math.Min(palette.Length, MaxBones);
        if (count <= 0)
            return;

        _gl.BindBuffer(BufferTargetARB.UniformBuffer, _boneUbo);
        fixed (Matrix4x4* ptr = palette)
        {
            _gl.BufferSubData(
                BufferTargetARB.UniformBuffer,
                0,
                (nuint)(count * sizeof(Matrix4x4)),
                ptr
            );
        }
        _gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteTexture(_defaultTexture);
        _gl.DeleteTexture(_defaultNormalTexture);
        _gl.DeleteBuffer(_boneUbo);
    }
}

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

        uniform mat4 uModel;
        uniform mat4 uViewProj;
        uniform mat3 uNormalMatrix;

        out vec3 vNormal;
        out vec2 vTexCoord;
        out vec3 vFragPos;
        out vec3 vTangent;

        void main() {
            vec4 worldPos = uModel * vec4(aPosition, 1.0);
            vFragPos  = worldPos.xyz;
            vNormal   = uNormalMatrix * aNormal;
            vTangent  = mat3(uModel) * aTangent;
            vTexCoord = aTexCoord;
            gl_Position = uViewProj * worldPos;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in  vec3 vNormal;
        in  vec2 vTexCoord;
        in  vec3 vFragPos;
        in  vec3 vTangent;
        out vec4 FragColor;

        uniform sampler2D uDiffuse;
        uniform sampler2D uNormalMap;
        uniform sampler2D uMetallicRoughnessMap;
        uniform sampler2D uAoMap;
        uniform sampler2D uEmissiveMap;
        uniform int       uHasNormalMap;
        uniform int       uHasMetallicRoughnessMap;
        uniform int       uHasAoMap;
        uniform int       uHasEmissiveMap;

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

        const float PI = 3.14159265359;

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
            vec3 halfDir = L + V;
            vec3 H = halfDir * inversesqrt(max(dot(halfDir, halfDir), 1e-10));

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

                vec3 radiance = uLightColor.rgb * uLightIntensity;

                vec3  F0  = mix(vec3(0.04), albedo, metallic);
                float NDF = distributionGGX(N, H, roughness);
                float G   = geometrySmith(N, V, L, roughness);
                vec3  F   = fresnelSchlick(max(dot(H, V), 0.0), F0);

                float NdotL = max(dot(N, L), 0.0);
                vec3  numerator = NDF * G * F;
                float denom = 4.0 * max(dot(N, V), 0.0) * NdotL + 1e-4;
                vec3  specular = numerator / denom;

                vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
                vec3 Lo = (kD * albedo / PI + specular) * radiance * NdotL;

                vec3 ambient = vec3(0.03) * albedo * ao;
                vec3 color = ambient + Lo + emissive;

                // Reinhard tone-map, then gamma encode back to sRGB.
                color = color / (color + vec3(1.0));
                color = pow(color, vec3(1.0 / 2.2));

                FragColor = vec4(color, rawTex.a * uDiffuseColor.a);
            } else {
                vec4 texColor = rawTex * uDiffuseColor;

                float diff = max(dot(N, L), 0.0);
                float spec = diff > 0.0 ? pow(max(dot(N, H), 0.0), uShininess) : 0.0;

                vec3 ambient  = (uAmbientColor * rawTex).rgb;
                vec3 diffuse  = texColor.rgb     * diff * uLightColor.rgb * uLightIntensity;
                vec3 specular = uSpecularColor.rgb * spec * uLightColor.rgb * uLightIntensity;

                FragColor = vec4(ambient + diffuse + specular, texColor.a);
            }
        }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _defaultTexture;
    private readonly uint _defaultNormalTexture;

    public Renderer3D(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);
        _defaultTexture = CreateWhiteTexture();
        _defaultNormalTexture = CreateFlatNormalTexture();
        SetSceneLighting(DirectionalLight.Default, Vector3.Zero);
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

    /// <summary>Draws a single mesh with the supplied transform and material.</summary>
    public void Draw(
        GpuMesh mesh,
        Matrix4x4 model,
        Matrix4x4 viewProj,
        Material3D material,
        TextureManager textures
    )
    {
        _shader.Bind();

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

        if (!string.IsNullOrEmpty(material.DiffuseTexturePath))
            textures.Get(material.DiffuseTexturePath).Bind(TextureUnit.Texture0);
        else
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        }

        _shader.SetUniformInt("uDiffuse", 0);

        if (!string.IsNullOrEmpty(material.NormalTexturePath))
        {
            textures.Get(material.NormalTexturePath).Bind(TextureUnit.Texture1);
            _shader.SetUniformInt("uHasNormalMap", 1);
        }
        else
        {
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultNormalTexture);
            _shader.SetUniformInt("uHasNormalMap", 0);
        }

        _shader.SetUniformInt("uNormalMap", 1);

        // Only the PBR branch samples the metallic-roughness/AO/emissive maps, so skip the
        // texture binds entirely for Blinn-Phong materials and just clear the has-flags.
        if (material.UsePbr)
        {
            BindOptionalTexture(
                textures,
                material.MetallicRoughnessTexturePath,
                TextureUnit.Texture2,
                2,
                "uMetallicRoughnessMap",
                "uHasMetallicRoughnessMap"
            );
            BindOptionalTexture(
                textures,
                material.AoTexturePath,
                TextureUnit.Texture3,
                3,
                "uAoMap",
                "uHasAoMap"
            );
            BindOptionalTexture(
                textures,
                material.EmissiveTexturePath,
                TextureUnit.Texture4,
                4,
                "uEmissiveMap",
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

    // Binds an optional PBR texture to the given unit (or a 1×1 white fallback when absent)
    // and sets the matching sampler + has-flag uniforms so the shader can branch on presence.
    private void BindOptionalTexture(
        TextureManager textures,
        string? path,
        TextureUnit unit,
        int samplerSlot,
        string samplerUniform,
        string hasUniform
    )
    {
        if (!string.IsNullOrEmpty(path))
        {
            textures.Get(path).Bind(unit);
            _shader.SetUniformInt(hasUniform, 1);
        }
        else
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
            _shader.SetUniformInt(hasUniform, 0);
        }

        _shader.SetUniformInt(samplerUniform, samplerSlot);
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

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteTexture(_defaultTexture);
        _gl.DeleteTexture(_defaultNormalTexture);
    }
}

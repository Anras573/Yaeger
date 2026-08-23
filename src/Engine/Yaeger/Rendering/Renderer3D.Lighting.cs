using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

// Directional, point, and spot light uniform upload — SetSceneLighting/SetAmbient/SetPointLights/
// SetSpotLights. See Renderer3D.cs for construction and Renderer3D.Shadows.cs for the (separate)
// shadow-casting uniforms these lights don't carry themselves.
public sealed partial class Renderer3D
{
    /// <summary>
    /// Maximum number of directional lights the fragment shader can accumulate per frame. Two, so a
    /// day/night cycle can light dawn and dusk with a sun and a moon at once — see docs/day-night.md.
    /// </summary>
    public const int MaxDirectionalLights = 2;

    /// <summary>Maximum number of point lights the fragment shader can accumulate per frame.</summary>
    public const int MaxPointLights = 16;

    /// <summary>Maximum number of spot lights the fragment shader can accumulate per frame.</summary>
    public const int MaxSpotLights = 8;

    // Per-light uniform names depend only on the array index, so build them once and reuse them
    // every frame. Interpolating them inside the per-frame upload loops would allocate a fresh
    // string per light field on every call.
    private static readonly string[] DirDirectionNames = BuildNames(
        "uDirLights",
        "direction",
        MaxDirectionalLights
    );
    private static readonly string[] DirColorNames = BuildNames(
        "uDirLights",
        "color",
        MaxDirectionalLights
    );
    private static readonly string[] DirIntensityNames = BuildNames(
        "uDirLights",
        "intensity",
        MaxDirectionalLights
    );
    private static readonly string[] PointPositionNames = BuildNames(
        "uPointLights",
        "position",
        MaxPointLights
    );
    private static readonly string[] PointColorNames = BuildNames(
        "uPointLights",
        "color",
        MaxPointLights
    );
    private static readonly string[] PointIntensityNames = BuildNames(
        "uPointLights",
        "intensity",
        MaxPointLights
    );
    private static readonly string[] PointRangeNames = BuildNames(
        "uPointLights",
        "range",
        MaxPointLights
    );
    private static readonly string[] SpotPositionNames = BuildNames(
        "uSpotLights",
        "position",
        MaxSpotLights
    );
    private static readonly string[] SpotDirectionNames = BuildNames(
        "uSpotLights",
        "direction",
        MaxSpotLights
    );
    private static readonly string[] SpotColorNames = BuildNames(
        "uSpotLights",
        "color",
        MaxSpotLights
    );
    private static readonly string[] SpotIntensityNames = BuildNames(
        "uSpotLights",
        "intensity",
        MaxSpotLights
    );
    private static readonly string[] SpotInnerCosNames = BuildNames(
        "uSpotLights",
        "innerCos",
        MaxSpotLights
    );
    private static readonly string[] SpotOuterCosNames = BuildNames(
        "uSpotLights",
        "outerCos",
        MaxSpotLights
    );
    private static readonly string[] SpotRangeNames = BuildNames(
        "uSpotLights",
        "range",
        MaxSpotLights
    );

    /// <summary>
    /// Uploads scene-wide lighting uniforms for a single directional light. Call once per frame
    /// before the draw loop. Equivalent to passing a one-element span to
    /// <see cref="SetSceneLighting(ReadOnlySpan{DirectionalLight}, Vector3)"/>.
    /// </summary>
    public void SetSceneLighting(DirectionalLight light, Vector3 cameraPos) =>
        SetSceneLighting(new ReadOnlySpan<DirectionalLight>(in light), cameraPos);

    /// <summary>
    /// Uploads scene-wide lighting uniforms for up to <see cref="MaxDirectionalLights"/> directional
    /// lights. Call once per frame before the draw loop. Lights past the cap are ignored; an empty
    /// span leaves the scene lit only by its point/spot lights and ambient.
    /// </summary>
    /// <remarks>
    /// Which light (if any) is shadowed is set separately by <see cref="SetShadowMap"/>'s
    /// <c>lightIndex</c>, indexing into this same span.
    /// </remarks>
    public void SetSceneLighting(ReadOnlySpan<DirectionalLight> lights, Vector3 cameraPos)
    {
        var count = Math.Min(lights.Length, MaxDirectionalLights);
        _shader.Bind();
        _shader.SetUniformInt("uDirLightCount", count);
        for (var i = 0; i < count; i++)
        {
            var light = lights[i];
            var lenSq = light.Direction.LengthSquared();
            var direction =
                float.IsFinite(lenSq) && lenSq > 0f
                    ? Vector3.Normalize(light.Direction)
                    : Vector3.UnitY;

            _shader.SetUniformVec3(DirDirectionNames[i], direction);
            _shader.SetUniformVec4(DirColorNames[i], light.Color.ToVector4());
            _shader.SetUniformFloat(DirIntensityNames[i], SanitizeNonNegative(light.Intensity));
        }
        _shader.SetUniformVec3("uCameraPos", cameraPos);
        _shader.Unbind();
    }

    /// <summary>
    /// Uploads the scene-wide ambient term used by the PBR path when image-based lighting is off.
    /// Call once per frame before the draw loop. The colour and intensity are combined here, so the
    /// shader reads a single pre-multiplied <c>vec3</c>.
    /// </summary>
    /// <remarks>
    /// Has no effect while an <see cref="EnvironmentMap"/> is bound via
    /// <see cref="SetEnvironmentMap"/> (IBL supplies its own, directional ambient), and none on the
    /// Blinn-Phong path, whose ambient is per-material — see <see cref="AmbientLight"/>.
    /// </remarks>
    public void SetAmbient(AmbientLight ambient)
    {
        var color = ambient.Color.ToVector4();
        var intensity = SanitizeNonNegative(ambient.Intensity);
        _shader.Bind();
        _shader.SetUniformVec3("uAmbientLight", new Vector3(color.X, color.Y, color.Z) * intensity);
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
}

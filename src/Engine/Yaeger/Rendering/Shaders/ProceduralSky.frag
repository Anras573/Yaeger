#version 330 core
in  vec3 vTexCoords;
out vec4 FragColor;

uniform vec3  uSunDirection;
uniform vec3  uMoonDirection;
uniform float uDaylightFactor;
uniform float uStarDensity;
uniform float uMoonPhase;
uniform float uCloudScale;
uniform float uCloudCoverage;
uniform vec2  uCloudOffset;
uniform mat3  uStarRotation;

const vec3 kDayZenith    = vec3(0.20, 0.45, 0.85);
const vec3 kDayHorizon   = vec3(0.75, 0.85, 0.95);
const vec3 kDuskZenith   = vec3(0.05, 0.05, 0.20);
const vec3 kDuskHorizon  = vec3(0.85, 0.45, 0.25);
const vec3 kNightZenith  = vec3(0.010, 0.015, 0.050);
const vec3 kNightHorizon = vec3(0.030, 0.040, 0.080);
const vec3 kGroundColor  = vec3(0.05, 0.05, 0.06);

const vec3 kSunColor        = vec3(1.00, 0.96, 0.85);
const vec3 kSunGlowColor    = vec3(1.00, 0.75, 0.45);
const vec3 kMoonColor       = vec3(0.85, 0.88, 0.95);
const vec3 kMoonGlowColor   = vec3(0.55, 0.60, 0.70);
const vec3 kCloudLitColor   = vec3(1.00, 0.98, 0.95);
const vec3 kCloudShadeColor = vec3(0.55, 0.58, 0.68);

const float kSunAngularRadius  = 0.03;  // radians, ~3.4 degrees across
const float kMoonAngularRadius = 0.045;

float hash13(vec3 p)
{
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash12(i);
    float b = hash12(i + vec2(1.0, 0.0));
    float c = hash12(i + vec2(0.0, 1.0));
    float d = hash12(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Five octaves of value noise, each doubling frequency and halving amplitude - enough to break up
// the single-frequency look without the cost growing unreasonably; the sky is drawn once per pixel
// per frame, not iterated like the particle turbulence field.
float cloudFbm(vec2 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        sum += amplitude * valueNoise(p);
        p *= 2.02;
        amplitude *= 0.5;
    }
    return sum;
}

float smoothStep01(float edge0, float edge1, float x)
{
    float t = clamp((x - edge0) / max(edge1 - edge0, 1e-5), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

vec3 safeAxis(vec3 v, vec3 fallback)
{
    float lengthSquared = dot(v, v);
    return lengthSquared > 1e-6 ? v * inversesqrt(lengthSquared) : fallback;
}

// Sky gradient for a view direction, blended day -> dusk -> night by sun elevation. "Dusk" is a
// band centred on the horizon crossing (sun elevation zero) rather than a night/day midpoint, since
// that's when the sky actually reads as sunset/sunrise-coloured; it fades out again toward both
// full day and full night.
vec3 skyGradient(vec3 dir, vec3 sunDir, float daylight)
{
    float dusk = 1.0 - smoothStep01(0.0, 0.35, abs(sunDir.y));

    vec3 zenith  = mix(mix(kNightZenith, kDuskZenith, dusk), kDayZenith, daylight);
    vec3 horizon = mix(mix(kNightHorizon, kDuskHorizon, dusk), kDayHorizon, daylight);

    if (dir.y >= 0.0)
    {
        float t = pow(clamp(dir.y, 0.0, 1.0), 0.55);
        return mix(horizon, zenith, t);
    }

    float t = clamp(-dir.y * 3.0, 0.0, 1.0);
    return mix(horizon, kGroundColor, t);
}

// A crisp disc plus a soft surrounding glow, for a body with no phase (the sun).
vec3 celestialDisc(vec3 dir, vec3 bodyDir, float angularRadius, vec3 color, vec3 glowColor)
{
    float alignment = dot(dir, bodyDir);
    if (alignment <= 0.0)
        return vec3(0.0);

    float edge = cos(angularRadius);
    float disc = smoothStep01(edge - 0.003, edge, alignment);
    float glow = pow(alignment, 64.0) * 0.35;
    return color * disc + glowColor * glow;
}

// Stylized lunar phase: a shadow disc the same size as the moon slides horizontally across it in
// the moon's own local frame. At phase 0.5 (full) the shadow sits fully off-disc; at 0/1 (new) it
// sits centred, covering the moon entirely. This isn't an astronomically exact terminator ellipse
// (see ProceduralSky.cs's remarks on MoonPhase) but it's symmetric and monotonic between the two,
// which is what a stylized sky needs.
float moonPhaseMask(vec3 dir, vec3 moonDir, float angularRadius, float phase)
{
    float alignment = dot(dir, moonDir);
    if (alignment <= 0.0)
        return 0.0;

    vec3 right = safeAxis(cross(vec3(0.0, 1.0, 0.0), moonDir), vec3(1.0, 0.0, 0.0));
    vec3 up = cross(moonDir, right);

    // Local disc coordinates: the small-angle projection is accurate enough at these angular sizes
    // and avoids an inverse-trig call per fragment.
    vec2 discUv = vec2(dot(dir, right), dot(dir, up)) / angularRadius;
    float r = length(discUv);
    if (r > 1.0)
        return 0.0;

    float t = clamp(phase, 0.0, 1.0) * 2.0 - 1.0; // 0 at full, +-1 at new
    float shadowSign = t == 0.0 ? 1.0 : sign(t);
    vec2 shadowCenter = vec2((1.0 - abs(t)) * 2.0 * shadowSign, 0.0);
    float shadow = 1.0 - smoothStep01(0.97, 1.03, length(discUv - shadowCenter));
    float disc = 1.0 - smoothStep01(0.96, 1.0, r);

    return disc * (1.0 - shadow);
}

// Hashes a view direction's star-grid cell to decide whether a star sits there and how bright it
// is. Scaling by a fixed grid density before flooring means each cell subtends a small, roughly
// constant angle regardless of view direction - good enough for point-like stars in a game sky,
// with the usual mild clustering near a unit cube's corners that this technique is known for.
float starField(vec3 dir, float density)
{
    vec3 cell = floor(dir * 180.0);
    float presence = hash13(cell);
    if (presence < density)
        return 0.0;

    float brightness = hash13(cell + 91.7);
    return mix(0.4, 1.0, brightness);
}

void main()
{
    vec3 dir = normalize(vTexCoords);
    vec3 color = skyGradient(dir, uSunDirection, uDaylightFactor);

    // Stars: rotated so the field wheels overhead, faded out by daylight so they're invisible by
    // day and only in the upper hemisphere (no stars "in the ground").
    float starVisibility = 1.0 - uDaylightFactor;
    if (starVisibility > 0.0 && dir.y > 0.0)
    {
        vec3 starDir = uStarRotation * dir;
        float star = starField(starDir, uStarDensity);
        color += vec3(star) * starVisibility;
    }

    // Clouds: a sky-dome planar projection (seamless by construction - it's a continuous function
    // of dir, not a wrapped UV atlas) of scrolling fBm noise, lit warm on the sun side and cool in
    // shadow, faded out near the horizon both to hide the projection's singularity there and
    // because a cloud layer silhouetted edge-on would otherwise read as a hard seam.
    if (dir.y > 0.02)
    {
        vec2 cloudUv = dir.xz / dir.y * uCloudScale + uCloudOffset;
        float density = cloudFbm(cloudUv);
        float coverageThreshold = 1.0 - clamp(uCloudCoverage, 0.0, 1.0);
        float cloud = smoothStep01(coverageThreshold, coverageThreshold + 0.25, density);

        float sunLit = clamp(dot(uSunDirection, vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5, 0.0, 1.0);
        vec3 cloudColor = mix(kCloudShadeColor, kCloudLitColor, sunLit);
        float horizonFade = smoothStep01(0.02, 0.18, dir.y);

        color = mix(color, cloudColor, cloud * horizonFade);
    }

    // Moon, then sun on top - the sun always wins on the rare frame the (never astronomically
    // accurate, near-opposite) two discs would overlap.
    float moon = moonPhaseMask(dir, uMoonDirection, kMoonAngularRadius, uMoonPhase);
    color += kMoonColor * moon;
    if (dot(dir, uMoonDirection) > cos(kMoonAngularRadius * 1.6))
        color += kMoonGlowColor * pow(max(dot(dir, uMoonDirection), 0.0), 64.0) * 0.15;

    color += celestialDisc(dir, uSunDirection, kSunAngularRadius, kSunColor, kSunGlowColor);

    FragColor = vec4(color, 1.0);
}

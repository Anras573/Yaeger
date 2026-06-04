using System.Numerics;

namespace Yaeger.Graphics;

public record struct DirectionalLight
{
    public Vector3 Direction; // points from the fragment toward the light source; need not be pre-normalised
    public Color Color;
    public float Intensity;
}

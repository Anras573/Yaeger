using System.Numerics;

namespace Yaeger.Graphics;

public record struct DirectionalLight
{
    public Vector3 Direction; // normalised; points from the fragment toward the light source
    public Color Color;
    public float Intensity;
}

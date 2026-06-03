using System.Numerics;

namespace Yaeger.Graphics;

public record struct DirectionalLight
{
    public Vector3 Direction; // normalised; points *toward* the light (not from surface)
    public Color Color;
    public float Intensity;
}

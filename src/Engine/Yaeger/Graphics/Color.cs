using System.Numerics;

namespace Yaeger.Graphics;

public readonly struct Color(byte r, byte g, byte b, byte a = 255)
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;
    public byte A { get; } = a;

    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color Green = new(0, 255, 0);
    public static readonly Color Blue = new(0, 0, 255);

    // Add more as needed

    /// <summary>
    /// Converts this color to a normalized Vector4 (0-1 range) suitable for shaders.
    /// </summary>
    public Vector4 ToVector4() => new(R / 255f, G / 255f, B / 255f, A / 255f);

    /// <summary>
    /// Creates a color from a normalized Vector4 (0-1 range), clamping each component to [0, 255].
    /// Inverse of <see cref="ToVector4"/>.
    /// </summary>
    public static Color FromVector4(Vector4 value) =>
        new(ToByte(value.X), ToByte(value.Y), ToByte(value.Z), ToByte(value.W));

    private static byte ToByte(float component) =>
        (byte)Math.Clamp((int)MathF.Round(component * 255f), 0, 255);
}

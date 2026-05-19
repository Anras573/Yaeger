namespace Yaeger.Graphics;

public readonly struct Sprite(string texturePath, Color? tint = null)
{
    public string TexturePath { get; } = texturePath;
    public Color Tint { get; } = tint ?? Color.White;
}

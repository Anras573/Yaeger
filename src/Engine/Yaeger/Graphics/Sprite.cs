namespace Yaeger.Graphics;

public readonly struct Sprite(string texturePath)
{
    public string TexturePath { get; } = texturePath;
}
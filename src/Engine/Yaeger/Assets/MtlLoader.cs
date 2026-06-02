using System.Globalization;
using Yaeger.Graphics;

namespace Yaeger.Assets;

public static class MtlLoader
{
    public static Dictionary<string, MtlMaterial> Load(string path)
    {
        var resolved = AssetPath.Resolve(path);

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"MTL file not found: {path}", resolved);

        var materials = new Dictionary<string, MtlMaterial>();

        string? currentName = null;
        string? diffuseTexturePath = null;
        Color ambientColor = Color.Black;
        Color diffuseColor = Color.White;
        Color specularColor = Color.Black;
        float shininess = 0f;

        void Flush()
        {
            if (currentName is null)
                return;
            materials[currentName] = new MtlMaterial(
                currentName,
                diffuseTexturePath,
                ambientColor,
                diffuseColor,
                specularColor,
                shininess
            );
        }

        foreach (var rawLine in File.ReadLines(resolved))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var spaceIdx = line.IndexOf(' ');
            var keyword = spaceIdx < 0 ? line : line[..spaceIdx];
            var rest = spaceIdx < 0 ? "" : line[(spaceIdx + 1)..].TrimStart();

            switch (keyword)
            {
                case "newmtl":
                    Flush();
                    currentName = rest;
                    diffuseTexturePath = null;
                    ambientColor = Color.Black;
                    diffuseColor = Color.White;
                    specularColor = Color.Black;
                    shininess = 0f;
                    break;
                case "Ka":
                    ambientColor = ParseColor(rest);
                    break;
                case "Kd":
                    diffuseColor = ParseColor(rest);
                    break;
                case "Ks":
                    specularColor = ParseColor(rest);
                    break;
                case "Ns":
                    shininess = float.Parse(rest, CultureInfo.InvariantCulture);
                    break;
                case "map_Kd":
                    diffuseTexturePath = rest;
                    break;
            }
        }

        Flush();
        return materials;
    }

    private static Color ParseColor(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new Color(
            ToChannel(float.Parse(parts[0], CultureInfo.InvariantCulture)),
            ToChannel(float.Parse(parts[1], CultureInfo.InvariantCulture)),
            ToChannel(float.Parse(parts[2], CultureInfo.InvariantCulture))
        );
    }

    private static byte ToChannel(float f) => (byte)Math.Clamp((int)(f * 255f), 0, 255);
}

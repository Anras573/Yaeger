using System.Collections.ObjectModel;
using System.Globalization;
using Yaeger.Graphics;

namespace Yaeger.Assets;

public static class MtlLoader
{
    public static IReadOnlyDictionary<string, MtlMaterial> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

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

            var commentIdx = line.IndexOf('#');
            if (commentIdx >= 0)
                line = line[..commentIdx].TrimEnd();
            if (line.Length == 0)
                continue;

            var wsIdx = line.IndexOfAny(s_whitespace);
            var keyword = wsIdx < 0 ? line : line[..wsIdx];
            var rest = wsIdx < 0 ? "" : line[(wsIdx + 1)..].TrimStart();

            switch (keyword)
            {
                case "newmtl":
                    Flush();
                    if (rest.Length == 0)
                        throw new FormatException(
                            "MTL 'newmtl' directive requires a non-empty material name."
                        );
                    currentName = rest;
                    diffuseTexturePath = null;
                    ambientColor = Color.Black;
                    diffuseColor = Color.White;
                    specularColor = Color.Black;
                    shininess = 0f;
                    break;
                case "Ka":
                    if (currentName is null)
                        throw new FormatException(
                            "MTL 'Ka' directive appears before any 'newmtl'."
                        );
                    ambientColor = ParseColor(rest);
                    break;
                case "Kd":
                    if (currentName is null)
                        throw new FormatException(
                            "MTL 'Kd' directive appears before any 'newmtl'."
                        );
                    diffuseColor = ParseColor(rest);
                    break;
                case "Ks":
                    if (currentName is null)
                        throw new FormatException(
                            "MTL 'Ks' directive appears before any 'newmtl'."
                        );
                    specularColor = ParseColor(rest);
                    break;
                case "Ns":
                    if (currentName is null)
                        throw new FormatException(
                            "MTL 'Ns' directive appears before any 'newmtl'."
                        );
                    shininess = float.Parse(rest, CultureInfo.InvariantCulture);
                    break;
                case "map_Kd":
                    if (currentName is null)
                        throw new FormatException(
                            "MTL 'map_Kd' directive appears before any 'newmtl'."
                        );
                    diffuseTexturePath = rest;
                    break;
            }
        }

        Flush();
        return new ReadOnlyDictionary<string, MtlMaterial>(materials);
    }

    private static readonly char[] s_whitespace = [' ', '\t'];

    private static Color ParseColor(string value)
    {
        var parts = value.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            throw new FormatException($"MTL color requires 3 components; got {parts.Length}.");
        return new Color(
            ToChannel(float.Parse(parts[0], CultureInfo.InvariantCulture)),
            ToChannel(float.Parse(parts[1], CultureInfo.InvariantCulture)),
            ToChannel(float.Parse(parts[2], CultureInfo.InvariantCulture))
        );
    }

    private static byte ToChannel(float f) => (byte)Math.Clamp((int)(f * 255f), 0, 255);
}

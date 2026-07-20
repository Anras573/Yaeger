namespace Yaeger.Rendering;

/// <summary>
/// Loads GLSL source from files under <c>Rendering/Shaders/</c> that are embedded into the
/// assembly at build time (see the <c>EmbeddedResource</c> item group in Yaeger.csproj). This
/// keeps each shader in its own file for editing/navigation while still requiring no runtime
/// file I/O — shaders remain part of the compiled assembly, same as the inline string literals
/// they replace.
/// </summary>
internal static class EmbeddedShaderSource
{
    public static string Load(string fileName)
    {
        var assembly = typeof(EmbeddedShaderSource).Assembly;
        var resourceName = $"Shaders.{fileName}";
        using var stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded shader resource '{resourceName}' not found."
            );
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

namespace Yaeger.Tests;

public class AssetPathTests
{
    [Fact]
    public void Resolve_ShouldReturnAbsolutePathUnchanged()
    {
        // Arrange
        var absolutePath = Path.Combine(Path.GetTempPath(), "Assets", "square.png");

        // Act
        var result = AssetPath.Resolve(absolutePath);

        // Assert
        Assert.Equal(absolutePath, result);
    }

    [Fact]
    public void Resolve_ShouldResolveRelativePathAgainstBaseDirectory()
    {
        // Arrange
        var relativePath = Path.Combine("Assets", "square.png");

        // Act
        var result = AssetPath.Resolve(relativePath);

        // Assert
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_ShouldReturnFullyNormalizedPath()
    {
        // Arrange
        var pathWithDotSegments = Path.Combine("Assets", "..", "Assets", "square.png");

        // Act
        var result = AssetPath.Resolve(pathWithDotSegments);

        // Assert
        Assert.DoesNotContain("..", result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void Resolve_ShouldReturnRootedPathForRelativeInput()
    {
        // Arrange
        var relativePath = "square.png";

        // Act
        var result = AssetPath.Resolve(relativePath);

        // Assert
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void Resolve_ShouldStartWithBaseDirectory()
    {
        // Arrange
        var relativePath = Path.Combine("Assets", "square.png");

        // Act
        var result = AssetPath.Resolve(relativePath);

        // Assert
        Assert.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), result);
    }
}

using Silk.NET.Input;
using Yaeger.Browser;
using Yaeger.Input;

namespace Yaeger.Tests.Input;

public class KeyMapperTests
{
    [Theory]
    [InlineData(Key.Number0, Keys.Num0)]
    [InlineData(Key.Number1, Keys.Num1)]
    [InlineData(Key.Number2, Keys.Num2)]
    [InlineData(Key.Number3, Keys.Num3)]
    [InlineData(Key.Number4, Keys.Num4)]
    [InlineData(Key.Number5, Keys.Num5)]
    [InlineData(Key.Number6, Keys.Num6)]
    [InlineData(Key.Number7, Keys.Num7)]
    [InlineData(Key.Number8, Keys.Num8)]
    [InlineData(Key.Number9, Keys.Num9)]
    public void TryGetMappedKey_ShouldMapNumberRow(Key key, Keys expected)
    {
        var mapped = KeyMapper.TryGetMappedKey(key, out var result);

        Assert.True(mapped);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryGetMappedKey_ShouldCoverEveryEngineKey()
    {
        var mapped = new HashSet<Keys>();
        foreach (var key in Enum.GetValues<Key>())
        {
            if (KeyMapper.TryGetMappedKey(key, out var result))
                mapped.Add(result);
        }

        var missing = Enum.GetValues<Keys>().Where(k => !mapped.Contains(k)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void GetDomCodes_ShouldCoverEveryEngineKey()
    {
        var missing = Enum.GetValues<Keys>()
            .Where(k => BrowserKeyMapper.GetDomCodes(k).Count == 0)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void GetDomCodes_ShouldNotShareACodeBetweenKeys()
    {
        var duplicates = Enum.GetValues<Keys>()
            .SelectMany(k => BrowserKeyMapper.GetDomCodes(k).Select(c => (Code: c, Key: k)))
            .GroupBy(x => x.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData(Keys.B, "KeyB")]
    [InlineData(Keys.Up, "ArrowUp")]
    [InlineData(Keys.Num5, "Digit5")]
    [InlineData(Keys.Shift, "ShiftRight")]
    [InlineData(Keys.Plus, "NumpadAdd")]
    [InlineData(Keys.F12, "F12")]
    public void GetDomCodes_ShouldContainExpectedCode(Keys key, string code)
    {
        Assert.Contains(code, BrowserKeyMapper.GetDomCodes(key));
    }
}

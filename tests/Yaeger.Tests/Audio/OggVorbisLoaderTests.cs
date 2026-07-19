using Silk.NET.OpenAL;
using Yaeger.Audio;

namespace Yaeger.Tests.Audio;

public class OggVorbisLoaderTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "Audio", "tone.ogg");

    [Fact]
    public void ResolveFormat_Mono_ShouldReturnMono16()
    {
        Assert.Equal(BufferFormat.Mono16, OggVorbisLoader.ResolveFormat(1));
    }

    [Fact]
    public void ResolveFormat_Stereo_ShouldReturnStereo16()
    {
        Assert.Equal(BufferFormat.Stereo16, OggVorbisLoader.ResolveFormat(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void ResolveFormat_UnsupportedChannelCount_ShouldThrow(int channels)
    {
        Assert.Throws<NotSupportedException>(() => OggVorbisLoader.ResolveFormat(channels));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, short.MaxValue)]
    [InlineData(-1f, -short.MaxValue)]
    public void WritePcm16_KnownValues_ShouldConvertCorrectly(float sample, short expected)
    {
        using var stream = new MemoryStream();

        OggVorbisLoader.WritePcm16(stream, [sample]);

        var bytes = stream.ToArray();
        Assert.Equal(2, bytes.Length);
        var actual = BitConverter.ToInt16(bytes, 0);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2f)] // beyond the valid range — must clamp, not overflow/wrap
    [InlineData(-2f)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void WritePcm16_OutOfRangeValues_ShouldClampRatherThanOverflow(float sample)
    {
        using var stream = new MemoryStream();

        OggVorbisLoader.WritePcm16(stream, [sample]);

        var actual = BitConverter.ToInt16(stream.ToArray(), 0);
        Assert.True(actual is short.MaxValue or -short.MaxValue);
    }

    [Fact]
    public void WritePcm16_MultipleSamples_ShouldWriteThemInOrder()
    {
        using var stream = new MemoryStream();

        OggVorbisLoader.WritePcm16(stream, [0f, 1f, -1f]);

        var bytes = stream.ToArray();
        Assert.Equal(6, bytes.Length);
        Assert.Equal(0, BitConverter.ToInt16(bytes, 0));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 2));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 4));
    }

    [Fact]
    public void LoadFully_MonoOggFixture_ShouldDecodeToMono16Pcm()
    {
        var (data, format, sampleRate) = OggVorbisLoader.LoadFully(FixturePath);

        // The fixture is a 0.2s, 8000 Hz, mono tone — 1600 samples of 16-bit PCM.
        Assert.Equal(BufferFormat.Mono16, format);
        Assert.Equal(8000, sampleRate);
        Assert.Equal(1600 * 2, data.Length);
    }

    [Fact]
    public void LoadFully_MissingFile_ShouldThrow()
    {
        var missingPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Audio", "nope.ogg");

        Assert.ThrowsAny<Exception>(() => OggVorbisLoader.LoadFully(missingPath));
    }
}

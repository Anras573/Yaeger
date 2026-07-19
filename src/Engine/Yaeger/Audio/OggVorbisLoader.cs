using System.Buffers.Binary;
using NVorbis;
using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Decodes OGG Vorbis files/streams via NVorbis (pure managed, no native dependency) into 16-bit
/// PCM, the format OpenAL is guaranteed to support without extensions.
/// </summary>
internal static class OggVorbisLoader
{
    /// <summary>
    /// Fully decodes an OGG Vorbis file into a single PCM byte array, for one-shot playback
    /// through <see cref="SoundBuffer"/> — the same path as a WAV file, just decoded first.
    /// </summary>
    public static (byte[] Data, BufferFormat Format, int SampleRate) LoadFully(string filePath)
    {
        using var reader = new VorbisReader(filePath);
        var format = ResolveFormat(reader.Channels);

        using var pcm = new MemoryStream();
        var scratch = new float[4096];
        int samplesRead;
        while ((samplesRead = reader.ReadSamples(scratch, 0, scratch.Length)) > 0)
        {
            WritePcm16(pcm, scratch.AsSpan(0, samplesRead));
        }

        return (pcm.ToArray(), format, reader.SampleRate);
    }

    /// <summary>
    /// Maps an OGG Vorbis channel count to the matching 16-bit OpenAL format.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown for anything other than mono or stereo.</exception>
    public static BufferFormat ResolveFormat(int channels) =>
        channels switch
        {
            1 => BufferFormat.Mono16,
            2 => BufferFormat.Stereo16,
            _ => throw new NotSupportedException(
                $"Unsupported channel count: {channels}. Only mono (1) and stereo (2) OGG Vorbis streams are supported."
            ),
        };

    /// <summary>
    /// Converts interleaved float PCM samples (NVorbis's native output, range [-1, 1]) to
    /// interleaved little-endian 16-bit PCM, appending to <paramref name="destination"/>.
    /// </summary>
    public static void WritePcm16(Stream destination, ReadOnlySpan<float> samples)
    {
        Span<byte> sampleBytes = stackalloc byte[2];
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            var value = (short)(clamped * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(sampleBytes, value);
            destination.Write(sampleBytes);
        }
    }
}

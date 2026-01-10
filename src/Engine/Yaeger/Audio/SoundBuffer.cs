using Silk.NET.OpenAL;

namespace Yaeger.Audio;

/// <summary>
/// Represents an audio buffer containing sound data.
/// </summary>
public sealed class SoundBuffer : IDisposable
{
    private readonly AL _al;
    private readonly uint _bufferId;
    private bool _disposed;

    private SoundBuffer(AL al, uint bufferId)
    {
        _al = al;
        _bufferId = bufferId;
    }

    /// <summary>
    /// Gets the OpenAL buffer ID.
    /// </summary>
    public uint BufferId => _bufferId;

    /// <summary>
    /// Creates a sound buffer from raw PCM audio data.
    /// </summary>
    /// <param name="context">The audio context.</param>
    /// <param name="data">The raw audio data.</param>
    /// <param name="format">The audio format (e.g., Mono16, Stereo16).</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    /// <returns>A new SoundBuffer instance.</returns>
    public static unsafe SoundBuffer Create(AudioContext context, ReadOnlySpan<byte> data, BufferFormat format, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        var bufferId = context.Al.GenBuffer();

        fixed (byte* dataPtr = data)
        {
            context.Al.BufferData(bufferId, format, dataPtr, data.Length, sampleRate);
        }

        return new SoundBuffer(context.Al, bufferId);
    }

    /// <summary>
    /// Loads a sound buffer from a WAV file.
    /// </summary>
    /// <param name="context">The audio context.</param>
    /// <param name="filePath">The path to the WAV file.</param>
    /// <returns>A new SoundBuffer instance.</returns>
    public static SoundBuffer FromFile(AudioContext context, string filePath)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Audio file not found: {filePath}");
        }

        var (data, format, sampleRate) = LoadWavFile(filePath);
        return Create(context, data, format, sampleRate);
    }

    private static (byte[] data, BufferFormat format, int sampleRate) LoadWavFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // Validate minimum WAV file size (44 bytes for minimal PCM WAV header)
        if (stream.Length < 44)
        {
            throw new InvalidDataException("File is too small to be a valid WAV file");
        }

        // Read RIFF header
        var riff = reader.ReadChars(4);
        if (new string(riff) != "RIFF")
        {
            throw new InvalidDataException("Invalid WAV file: missing RIFF header");
        }

        reader.ReadInt32(); // File size
        var wave = reader.ReadChars(4);
        if (new string(wave) != "WAVE")
        {
            throw new InvalidDataException("Invalid WAV file: missing WAVE header");
        }

        // Read fmt chunk
        var fmt = reader.ReadChars(4);
        if (new string(fmt) != "fmt ")
        {
            throw new InvalidDataException("Invalid WAV file: missing fmt chunk");
        }

        var fmtSize = reader.ReadInt32();
        var audioFormat = reader.ReadInt16();
        var numChannels = reader.ReadInt16();
        var sampleRate = reader.ReadInt32();
        reader.ReadInt32(); // Byte rate
        reader.ReadInt16(); // Block align
        var bitsPerSample = reader.ReadInt16();

        // Validate audio format (1 = PCM)
        if (audioFormat != 1)
        {
            throw new NotSupportedException($"Unsupported WAV audio format: {audioFormat}. Only PCM (format 1) is supported.");
        }

        // Skip any extra fmt data
        if (fmtSize > 16)
        {
            reader.ReadBytes(fmtSize - 16);
        }

        // Find data chunk
        while (stream.Position < stream.Length)
        {
            // Ensure we have enough bytes to read chunk ID and size
            if (stream.Position + 8 > stream.Length)
            {
                break; // Not enough bytes for another chunk
            }

            var chunkId = reader.ReadChars(4);
            var chunkSize = reader.ReadInt32();

            // Validate that chunk size doesn't exceed remaining stream length
            if (stream.Position + chunkSize > stream.Length)
            {
                throw new InvalidDataException($"Invalid chunk size: {chunkSize} bytes exceeds remaining file size");
            }

            if (new string(chunkId) == "data")
            {
                var data = reader.ReadBytes(chunkSize);

                // WAV chunks should be aligned to even byte boundaries
                // If chunk size is odd, skip the padding byte
                if (chunkSize % 2 == 1 && stream.Position < stream.Length)
                {
                    reader.ReadByte();
                }

                // Determine format
                BufferFormat format;
                if (numChannels == 1 && bitsPerSample == 8)
                    format = BufferFormat.Mono8;
                else if (numChannels == 1 && bitsPerSample == 16)
                    format = BufferFormat.Mono16;
                else if (numChannels == 2 && bitsPerSample == 8)
                    format = BufferFormat.Stereo8;
                else if (numChannels == 2 && bitsPerSample == 16)
                    format = BufferFormat.Stereo16;
                else
                    throw new NotSupportedException($"Unsupported WAV format: {numChannels} channels, {bitsPerSample} bits per sample");

                return (data, format, sampleRate);
            }
            else
            {
                // Skip unknown chunks
                reader.ReadBytes(chunkSize);
                
                // WAV chunks should be aligned to even byte boundaries
                // If chunk size is odd, skip the padding byte
                if (chunkSize % 2 == 1 && stream.Position < stream.Length)
                {
                    reader.ReadByte();
                }
            }
        }

        throw new InvalidDataException("Invalid WAV file: missing data chunk");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _al.DeleteBuffer(_bufferId);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
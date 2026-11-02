using System.Numerics;
using Silk.NET.OpenAL;
using Yaeger.Audio;
using AudioContext = Yaeger.Audio.AudioContext;

namespace Yaeger.Tests.Audio;

public class SilentAudioContextTests
{
    private static SoundBuffer MakeBuffer(AudioContext context) =>
        SoundBuffer.Create(context, new byte[64], BufferFormat.Mono16, 44100);

    [Fact]
    public void CreateSilent_ShouldReportSilent()
    {
        using var context = AudioContext.CreateSilent();

        Assert.True(context.IsSilent);
    }

    [Fact]
    public void SoundSource_ShouldRoundTripProperties()
    {
        using var context = AudioContext.CreateSilent();
        using var source = SoundSource.Create(context);

        source.Looping = true;
        source.Pitch = 1.5f;
        source.Position = new Vector3(1, 2, 3);
        source.Gain = 0.25f;

        Assert.True(source.Looping);
        Assert.Equal(1.5f, source.Pitch);
        Assert.Equal(new Vector3(1, 2, 3), source.Position);
        Assert.Equal(0.25f, source.Gain);
    }

    [Fact]
    public void SoundSource_PlayNonLooping_ShouldFinishImmediately()
    {
        using var context = AudioContext.CreateSilent();
        using var buffer = MakeBuffer(context);
        using var source = SoundSource.Create(context);
        source.SetBuffer(buffer);

        source.Play();

        Assert.Equal(SourceState.Stopped, source.GetState());
    }

    [Fact]
    public void SoundSource_PlayLooping_ShouldStayPlayingUntilStopped()
    {
        using var context = AudioContext.CreateSilent();
        using var buffer = MakeBuffer(context);
        using var source = SoundSource.Create(context);
        source.SetBuffer(buffer);
        source.Looping = true;

        source.Play();
        var playing = source.GetState();
        source.Pause();
        var paused = source.GetState();
        source.Stop();

        Assert.Equal(SourceState.Playing, playing);
        Assert.Equal(SourceState.Paused, paused);
        Assert.Equal(SourceState.Stopped, source.GetState());
    }

    [Fact]
    public void SoundBuffer_Create_ShouldReturnDistinctIds()
    {
        using var context = AudioContext.CreateSilent();
        using var a = MakeBuffer(context);
        using var b = MakeBuffer(context);

        Assert.NotEqual(a.BufferId, b.BufferId);
    }

    [Fact]
    public void DistanceModel_ShouldRoundTrip()
    {
        using var context = AudioContext.CreateSilent();

        context.DistanceModel = DistanceModel.LinearDistance;

        Assert.Equal(DistanceModel.LinearDistance, context.DistanceModel);
    }

    [Fact]
    public void Dispose_ShouldBeSafeToCallTwice()
    {
        var context = AudioContext.CreateSilent();

        context.Dispose();
        context.Dispose();
    }
}

using Yaeger.Audio;

namespace Yaeger.Tests.Audio;

public class AudioMixerTests
{
    [Fact]
    public void Constructor_ShouldDefaultAllVolumesToOne()
    {
        var mixer = new AudioMixer();

        Assert.Equal(1f, mixer.MasterVolume);
        Assert.Equal(1f, mixer.MusicVolume);
        Assert.Equal(1f, mixer.SfxVolume);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    [InlineData(0.5f, 0.5f)]
    public void MasterVolume_ShouldClampToZeroOne(float input, float expected)
    {
        var mixer = new AudioMixer { MasterVolume = input };

        Assert.Equal(expected, mixer.MasterVolume);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public void MusicVolume_ShouldClampToZeroOne(float input, float expected)
    {
        var mixer = new AudioMixer { MusicVolume = input };

        Assert.Equal(expected, mixer.MusicVolume);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public void SfxVolume_ShouldClampToZeroOne(float input, float expected)
    {
        var mixer = new AudioMixer { SfxVolume = input };

        Assert.Equal(expected, mixer.SfxVolume);
    }

    [Fact]
    public void GetGroupMultiplier_Music_ShouldMultiplyMasterAndMusic()
    {
        var mixer = new AudioMixer
        {
            MasterVolume = 0.5f,
            MusicVolume = 0.4f,
            SfxVolume = 0.9f,
        };

        Assert.Equal(0.2f, mixer.GetGroupMultiplier(AudioGroup.Music), 0.0001f);
    }

    [Fact]
    public void GetGroupMultiplier_Sfx_ShouldMultiplyMasterAndSfx()
    {
        var mixer = new AudioMixer
        {
            MasterVolume = 0.5f,
            MusicVolume = 0.4f,
            SfxVolume = 0.9f,
        };

        Assert.Equal(0.45f, mixer.GetGroupMultiplier(AudioGroup.Sfx), 0.0001f);
    }

    [Fact]
    public void VolumeChanged_ShouldFireWhenMasterVolumeChanges()
    {
        var mixer = new AudioMixer();
        var fired = 0;
        mixer.VolumeChanged += () => fired++;

        mixer.MasterVolume = 0.5f;

        Assert.Equal(1, fired);
    }

    [Fact]
    public void VolumeChanged_ShouldFireWhenMusicVolumeChanges()
    {
        var mixer = new AudioMixer();
        var fired = 0;
        mixer.VolumeChanged += () => fired++;

        mixer.MusicVolume = 0.3f;

        Assert.Equal(1, fired);
    }

    [Fact]
    public void VolumeChanged_ShouldFireWhenSfxVolumeChanges()
    {
        var mixer = new AudioMixer();
        var fired = 0;
        mixer.VolumeChanged += () => fired++;

        mixer.SfxVolume = 0.3f;

        Assert.Equal(1, fired);
    }

    [Fact]
    public void VolumeChanged_ShouldNotFireWhenSettingTheSameValue()
    {
        var mixer = new AudioMixer { MasterVolume = 0.7f };
        var fired = 0;
        mixer.VolumeChanged += () => fired++;

        mixer.MasterVolume = 0.7f;

        Assert.Equal(0, fired);
    }

    [Fact]
    public void VolumeChanged_ShouldNotFireWhenClampingProducesTheSameStoredValue()
    {
        // Setting to 1 (already the default) via an out-of-range value that clamps back to 1
        // should not be treated as a change.
        var mixer = new AudioMixer();
        var fired = 0;
        mixer.VolumeChanged += () => fired++;

        mixer.MasterVolume = 5f; // clamps to 1, same as the current value

        Assert.Equal(0, fired);
    }
}

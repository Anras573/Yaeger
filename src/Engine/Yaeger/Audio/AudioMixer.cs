namespace Yaeger.Audio;

/// <summary>
/// Master/music/SFX volume groups, shared by every <see cref="SoundSource"/> and
/// <see cref="StreamingSoundSource"/> created through the same <see cref="AudioContext"/>
/// (<see cref="AudioContext.Mixer"/>). A source's actual OpenAL gain is
/// <c>source.Gain * MasterVolume * (group-specific volume)</c>; changing a mixer volume fires
/// <see cref="VolumeChanged"/>, which every live source subscribes to so the change takes effect
/// immediately, without needing to touch the source itself.
/// </summary>
public sealed class AudioMixer
{
    private float _masterVolume = 1f;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;

    /// <summary>
    /// Fired whenever <see cref="MasterVolume"/>, <see cref="MusicVolume"/>, or
    /// <see cref="SfxVolume"/> actually changes value.
    /// </summary>
    public event Action? VolumeChanged;

    /// <summary>Multiplier applied on top of every group. Clamped to [0, 1]. Defaults to 1.</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set => SetVolume(ref _masterVolume, value);
    }

    /// <summary>Multiplier applied to sources in <see cref="AudioGroup.Music"/>. Clamped to [0, 1]. Defaults to 1.</summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set => SetVolume(ref _musicVolume, value);
    }

    /// <summary>Multiplier applied to sources in <see cref="AudioGroup.Sfx"/>. Clamped to [0, 1]. Defaults to 1.</summary>
    public float SfxVolume
    {
        get => _sfxVolume;
        set => SetVolume(ref _sfxVolume, value);
    }

    /// <summary>
    /// The combined master × group-specific multiplier for <paramref name="group"/>, to be
    /// multiplied by a source's own <c>Gain</c> to get the value actually sent to OpenAL.
    /// </summary>
    public float GetGroupMultiplier(AudioGroup group) =>
        _masterVolume
        * group switch
        {
            AudioGroup.Music => _musicVolume,
            AudioGroup.Sfx => _sfxVolume,
            _ => 1f,
        };

    private void SetVolume(ref float field, float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        if (field == clamped)
            return;

        field = clamped;
        VolumeChanged?.Invoke();
    }
}

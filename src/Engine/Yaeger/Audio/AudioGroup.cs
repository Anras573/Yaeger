namespace Yaeger.Audio;

/// <summary>
/// The volume group a <see cref="SoundSource"/> or <see cref="StreamingSoundSource"/> belongs to,
/// used by <see cref="AudioMixer"/> to apply a group-wide gain multiplier on top of each source's
/// own <c>Gain</c>.
/// </summary>
public enum AudioGroup
{
    Music,
    Sfx,
}

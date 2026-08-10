namespace Yaeger.Audio;

/// <summary>
/// Attach alongside a <see cref="Yaeger.Graphics.Transform3D"/> (or a
/// <see cref="Yaeger.Graphics.Transform2D"/> — see <see cref="Yaeger.Systems.AudioSystem"/>'s 2D
/// convenience mapping) to have <see cref="Yaeger.Systems.AudioSystem"/> play
/// <see cref="Buffer"/> spatialized at the entity's position: panned and attenuated by distance
/// from the active listener (the first <c>Camera3D</c> entity, or failing that the first
/// <c>Camera2D</c> entity, or the world origin).
/// </summary>
/// <remarks>
/// <see cref="MinDistance"/>/<see cref="MaxDistance"/>/<see cref="RolloffFactor"/> map directly
/// onto this source's OpenAL reference distance / max distance / rolloff factor, layered on top
/// of the distance model configured once on <see cref="AudioContext"/>. <see cref="Volume"/> is
/// this source's own logical gain — like <see cref="SoundSource.Gain"/>, the value actually sent
/// to OpenAL also factors in <see cref="AudioContext.Mixer"/>'s <see cref="AudioGroup.Sfx"/>
/// multiplier, so master/SFX volume changes apply to spatialized sources too.
/// <para>
/// Playback starts automatically the first update after the component is attached (or after
/// <see cref="Buffer"/> changes to a different instance), looping continuously when
/// <see cref="Loop"/> is set or otherwise playing once. A <c>null</c> <see cref="Buffer"/> is a
/// no-op until one is assigned. Removing the component (or destroying the entity) stops and
/// releases the underlying OpenAL source on the next <see cref="Yaeger.Systems.AudioSystem"/>
/// update.
/// </para>
/// </remarks>
public record struct AudioSource3D(
    SoundBuffer? Buffer,
    bool Loop = false,
    float Volume = 1f,
    float MinDistance = 1f,
    float MaxDistance = 50f,
    float RolloffFactor = 1f
);

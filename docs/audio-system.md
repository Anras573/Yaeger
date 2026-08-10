# Audio System Documentation

## Overview

The Yaeger engine includes a sound system built on OpenAL through Silk.NET. It plays WAV and OGG
Vorbis audio, either fully decoded into memory (short SFX) or streamed from a ring of buffers
(background music), with master/music/SFX volume groups on top.

## Features

- **Automatic Initialization**: The audio context is automatically created when you create a Window
- **WAV and OGG Vorbis support**: Fully-decoded playback for both; OGG decoding is pure managed (NVorbis), no native dependency
- **Streaming**: `StreamingSoundSource` streams an OGG file through a small ring of buffers instead of decoding the whole thing into memory — the right choice for music
- **Volume groups**: `AudioMixer` applies master/music/SFX multipliers on top of each source's own volume, and changes take effect on already-playing sources immediately
- **Positional (3D) audio**: `AudioSource3D` + `AudioSystem` pan and attenuate a sound by its entity's position, with the listener tracking the active `Camera3D`/`Camera2D`
- **Flexible Playback**: Control volume, pitch, looping, and 3D positioning
- **Resource Management**: Proper disposal of audio resources

## Basic Usage

### 1. Load a Sound

```csharp
// The audio context is available from the window
var audioContext = window.AudioContext;

// Load a WAV or OGG file into a buffer — dispatched by extension
var soundBuffer = SoundBuffer.FromFile(audioContext, "Assets/beep.wav");
```

### 2. Create a Sound Source

```csharp
// Create a source to play the sound
var soundSource = SoundSource.Create(audioContext);

// Set the buffer to play
soundSource.SetBuffer(soundBuffer);
```

### 3. Play the Sound

```csharp
// Play the sound
soundSource.Play();

// You can also pause or stop
soundSource.Pause();
soundSource.Stop();
```

## Advanced Features

### Volume Control

```csharp
// Set volume (0.0 to 1.0)
soundSource.Gain = 0.5f; // 50% volume
```

### Pitch Control

```csharp
// Set pitch (1.0 is normal, 2.0 is double speed/pitch)
soundSource.Pitch = 1.2f;
```

### Looping

```csharp
// Enable looping for background music
soundSource.Looping = true;
soundSource.Play();
```

### Manual positioning on a plain SoundSource

`SoundSource.Position`/`Velocity` are still there for low-level control, but a `SoundSource` is
created listener-relative (see "Positional (3D) audio" below), so `Position` is an offset from the
listener rather than an absolute world position:

```csharp
using System.Numerics;

// Offsets the sound 10 units from the listener, not from world-space origin.
soundSource.Position = new Vector3(10f, 0f, 0f);

// Set velocity for Doppler effect
soundSource.Velocity = new Vector3(1f, 0f, 0f);
```

For a sound that should pan/attenuate by an entity's actual world position — a torch, an engine,
footsteps — use `AudioSource3D` + `AudioSystem` instead; see "Positional (3D) audio" below.

### Check Playback State

```csharp
var state = soundSource.GetState();
if (state == SourceState.Playing)
{
    Console.WriteLine("Sound is playing");
}
```

## Complete Example

```csharp
using Yaeger.Audio;
using Yaeger.Windowing;

// Create window (audio context is initialized automatically)
using var window = Window.Create();

// Load sound effects
var bounceSound = SoundBuffer.FromFile(window.AudioContext, "Assets/bounce.wav");
var scoreSound = SoundBuffer.FromFile(window.AudioContext, "Assets/score.wav");

// Create sound sources
var bounceSrc = SoundSource.Create(window.AudioContext);
bounceSrc.SetBuffer(bounceSound);

var scoreSrc = SoundSource.Create(window.AudioContext);
scoreSrc.SetBuffer(scoreSound);

// In your game logic:
void OnBallHitPaddle()
{
    bounceSrc.Play();
}

void OnScore()
{
    scoreSrc.Play();
}

// Cleanup is automatic when window is disposed
```

## Streaming background music

A fully-decoded multi-minute OGG track would sit in memory at roughly 10 MB per minute of
stereo 16-bit audio. `StreamingSoundSource` avoids that by decoding a small ring of 4 buffers
ahead of playback and refilling them as OpenAL finishes with each one:

```csharp
var music = StreamingSoundSource.FromFile(window.AudioContext, "Assets/music.ogg");
music.Looping = true;
music.Gain = 0.3f;
music.Play();

window.OnUpdate += _ => music.Update(); // pump the stream every frame
```

`Update()` checks how many queued buffers OpenAL has finished playing, decodes the next chunk of
the OGG stream into each one, and re-queues it — call it regularly (once per frame is plenty; the
ring holds a few hundred milliseconds of buffered audio, a large margin over a single frame's
worth of time). With `Looping = true`, reaching the end of the stream seeks back to sample 0 and
keeps decoding immediately, mid-chunk if necessary, so the loop point doesn't produce a gap.
`Stop()` rewinds the stream too, so a later `Play()` restarts from the beginning.

`StreamingSoundSource` only supports OGG Vorbis (not WAV) and only mono/stereo streams, same as
`SoundBuffer`. For short one-shot SFX, keep using `SoundBuffer`/`SoundSource` — OGG works there
too (see below), just fully decoded rather than streamed.

## Volume groups

`AudioContext.Mixer` (an `AudioMixer`) exposes three multipliers, each clamped to `[0, 1]` and
defaulting to 1:

```csharp
window.AudioContext.Mixer.MasterVolume = 0.8f;
window.AudioContext.Mixer.MusicVolume = 0.5f;
window.AudioContext.Mixer.SfxVolume = 1.0f;
```

Every `SoundSource`/`StreamingSoundSource` belongs to an `AudioGroup` (`Music` or `Sfx`, chosen
when you create it — `SoundSource.Create` defaults to `Sfx`, `StreamingSoundSource.FromFile`
defaults to `Music`):

```csharp
var music = StreamingSoundSource.FromFile(window.AudioContext, "Assets/music.ogg", AudioGroup.Music);
var jump = SoundSource.Create(window.AudioContext, AudioGroup.Sfx);
```

A source's own `Gain` is its logical volume; the value actually sent to OpenAL is
`Gain * MasterVolume * (that group's volume)`. Changing a mixer volume applies to every
already-playing source in that group immediately — you don't need to touch the sources
themselves. Because of this, reading a source's `Gain` back returns the logical value you set,
not whatever mixed value OpenAL currently has applied.

## Loading Background Music (fully decoded)

For a short music loop where streaming isn't necessary, the regular `SoundBuffer` path still
works with either format:

```csharp
// Load music (WAV or OGG)
var musicBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/music.wav");
var musicSource = SoundSource.Create(window.AudioContext, AudioGroup.Music);
musicSource.SetBuffer(musicBuffer);

// Set it to loop and lower the volume
musicSource.Looping = true;
musicSource.Gain = 0.3f;

// Start playing
musicSource.Play();
```

## Supported Audio Formats

- **WAV**: mono or stereo, 8-bit or 16-bit PCM, any standard sample rate (44100 Hz recommended)
- **OGG Vorbis**: mono or stereo, any sample rate the file specifies — decoded via NVorbis (pure
  managed, no native dependency) into 16-bit PCM, since that's the format OpenAL is guaranteed to
  support without extensions. Not supported: MP3, and anything beyond mono/stereo.

## Resource Management

Audio resources are automatically cleaned up when the window is disposed. However, you can also manually dispose of resources:

```csharp
// Manual cleanup if needed
soundSource.Dispose();
soundBuffer.Dispose();
streamingSource.Dispose(); // also disposes the underlying OGG decoder
```

## Implementation Notes

- The audio context is created during window initialization
- Only one audio context exists per window, and it owns the single shared `AudioMixer`
- OpenAL manages the audio device and context lifecycle
- Audio operations must be performed from the thread that owns the OpenAL context (typically the window's main thread); thread safety across multiple threads is not guaranteed by the engine
- `StreamingSoundSource.Update()` must be called from that same thread, regularly, for the stream to keep up with playback

## Positional (3D) audio

`AudioSource3D` + `AudioSystem` spatialize a sound by an entity's position, using OpenAL's
built-in distance attenuation and stereo panning:

```csharp
using Yaeger.Audio;
using Yaeger.Systems;

var buffer = SoundBuffer.FromFile(window.AudioContext, "Assets/torch.wav");
var audioSystem = new AudioSystem(world, window.AudioContext);

var torch = world.CreateEntity();
world.AddComponent(torch, new Transform3D(new Vector3(4f, 1f, 0f), Quaternion.Identity, Vector3.One));
world.AddComponent(
    torch,
    new AudioSource3D(buffer, Loop: true, Volume: 0.7f, MinDistance: 2f, MaxDistance: 20f, RolloffFactor: 1f)
);

window.OnUpdate += deltaTime => audioSystem.Update((float)deltaTime);
```

Playback starts automatically the first update after `AudioSource3D` is attached (or after its
`Buffer` changes to a different instance) — looping continuously when `Loop` is set, or playing
once otherwise. A `null` `Buffer` is a no-op until one is assigned. Removing the component (or
destroying the entity) stops and releases the underlying OpenAL source on the next
`AudioSystem.Update`; disposing `AudioSystem` itself releases every source it still owns.

`MinDistance`/`MaxDistance`/`RolloffFactor` map directly onto OpenAL's per-source reference
distance / max distance / rolloff factor, layered on top of the distance model configured once on
`AudioContext.DistanceModel` (defaults to `DistanceModel.InverseDistanceClamped`, matching
OpenAL's own default — override it once, globally, if a game wants a different falloff curve).
`Volume` is this source's own logical gain, same as `SoundSource.Gain` — the value actually sent
to OpenAL also factors in `AudioContext.Mixer`'s SFX/master multipliers, so volume-group changes
apply to spatialized sources exactly like non-positional ones (see "Volume groups" above).

### The listener

Each `AudioSystem.Update` also points the OpenAL listener at the scene's active camera, in
priority order:

1. The first `Camera3D` entity — listener position is the camera's `Position`; orientation (at/up)
   is derived from the camera's `ViewMatrix` (see `AudioSpatialMath.ExtractOrientation`).
2. Otherwise, the first `Camera2D` entity — listener position is the camera's `Position` mapped
   onto the Z=0 plane, with a fixed orientation (facing into the screen, +Y up).
3. Otherwise, the world origin with that same fixed 2D orientation.

An `AudioSource3D` entity is positioned the same way: a `Transform3D` places it in full 3D; an
entity with only a `Transform2D` is mapped onto that same Z=0 plane (`AudioSystem.ToListenerPlane`)
— both the source and a `Camera2D` listener live on it, so panning/attenuation for a 2D game is
driven entirely by XY offset, letting a top-down or side-view 2D game get panned/attenuated SFX
without any 3D setup.

### Interaction with non-positional playback

`SoundSource`/`StreamingSoundSource` (the plain, non-positional path — UI sounds, music) are
unaffected by any of the above: both are created listener-relative (OpenAL's
`AL_SOURCE_RELATIVE`), anchored at the listener regardless of where `AudioSystem` moves it, so
they stay full-volume and centered exactly as before this feature existed. `AudioSource3D`'s
sources are created with absolute world positions instead, which is what real spatialization
needs — `SoundSource.Create`'s `listenerRelative` parameter is what selects between the two, and
`AudioSystem` is the only caller that passes `false`.

### CPU-side math

The math behind the listener's orientation (`AudioSpatialMath.ExtractOrientation`) and the 2D→3D
position mapping (`AudioSpatialMath.ToListenerPlane`) is pure C# with no OpenAL dependency, so
it's unit-tested directly (`AudioSpatialMathTests`) — unlike the rest of this system, which needs
a live audio device and stays untested per the repo's test conventions.

See `Samples/DamagedHelmet` for a working example: a looping hum on the helmet pans and
attenuates as the camera orbits around it.

## Out of scope

- MP3 (patent-free OGG Vorbis covers the streaming/compressed-audio need)
- HRTF/binaural configuration, audio effects and filters (reverb zones, occlusion)
- Doppler tuning beyond OpenAL's own defaults (`SoundSource.Velocity`/`AudioSource3D` don't set
  per-source Doppler factors)
- Streaming positional audio (`StreamingSoundSource` stays listener-relative only; use
  `AudioSource3D`, which is buffer-backed, for spatialized sound)

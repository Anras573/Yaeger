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

### 3D Audio Positioning

```csharp
using System.Numerics;

// Position the sound in 3D space
soundSource.Position = new Vector3(10f, 0f, 0f);

// Set velocity for Doppler effect
soundSource.Velocity = new Vector3(1f, 0f, 0f);
```

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

## Out of scope

- MP3 (patent-free OGG Vorbis covers the streaming/compressed-audio need)
- Positional/3D audio beyond the existing per-source `Position`/`Velocity` (no listener orientation, no attenuation model)
- Audio effects and filters

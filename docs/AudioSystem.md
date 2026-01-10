# Audio System Documentation

## Overview

The Yaeger engine now includes a sound system built on OpenAL through Silk.NET. This allows you to play audio files (WAV format) in your games.

## Features

- **Automatic Initialization**: The audio context is automatically created when you create a Window
- **WAV File Support**: Built-in support for loading and playing WAV audio files
- **Flexible Playback**: Control volume, pitch, looping, and 3D positioning
- **Resource Management**: Proper disposal of audio resources

## Basic Usage

### 1. Load a Sound

```csharp
// The audio context is available from the window
var audioContext = window.AudioContext;

// Load a WAV file into a buffer
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

## Loading Background Music

```csharp
// Load music
var musicBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/music.wav");
var musicSource = SoundSource.Create(window.AudioContext);
musicSource.SetBuffer(musicBuffer);

// Set it to loop and lower the volume
musicSource.Looping = true;
musicSource.Gain = 0.3f;

// Start playing
musicSource.Play();
```

## Supported Audio Formats

The current implementation supports WAV files with the following specifications:

- **Channels**: Mono (1 channel) or Stereo (2 channels)
- **Bit Depth**: 8-bit or 16-bit
- **Sample Rate**: Any standard rate (44100 Hz recommended)

## Resource Management

Audio resources are automatically cleaned up when the window is disposed. However, you can also manually dispose of resources:

```csharp
// Manual cleanup if needed
soundSource.Dispose();
soundBuffer.Dispose();
```

## Implementation Notes

- The audio context is created during window initialization
- Only one audio context exists per window
- OpenAL manages the audio device and context lifecycle
- All audio operations are thread-safe through OpenAL

## Future Enhancements

The following features may be added in future updates:

- Support for additional audio formats (MP3, OGG)
- Audio streaming for large files
- Audio effects and filters
- 3D audio listener positioning
- Audio mixing capabilities

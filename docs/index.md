# Yaeger

**Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

Yaeger is a modular, experimental 2D game engine written in C#. It provides a flexible and extensible platform for rapid prototyping and development of games and interactive applications.

## Features

- **Entity-Component-System (ECS)** architecture
- **2D rendering** with Silk.NET
- **Batch rendering** for efficient sprite rendering
- **Animation system** with frame-based texture cycling
- **Audio system** with OpenAL support
- **Input handling** (keyboard, mouse)
- Extensible component and system design

## Quick Start

### Prerequisites

- .NET 9.0 SDK or later
- Compatible OS (Windows, macOS, Linux)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/Anras573/Yaeger.git
cd Yaeger

# Build the engine and samples
dotnet build yaeger.sln

# Run the Pong sample
dotnet run --project Samples/Pong/Pong.csproj
```

## Project Structure

```
src/Engine/Yaeger/
├── ECS/        # Entity-Component-System framework
├── Rendering/  # Rendering systems (including batch renderer)
├── Graphics/   # Graphics primitives and utilities
├── Audio/      # Audio system (OpenAL)
├── Input/      # Input handling
└── Windowing/  # Window management

Samples/
├── Pong/                    # Classic Pong game
├── BatchRenderingExample/   # Batch rendering demo
└── TextRenderingExample/    # Text rendering demo
```

## License

This project is licensed under the MIT License.

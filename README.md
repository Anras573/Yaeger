# Yaeger

YAEGER - **Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

## Overview

Yaeger is a modular, experimental 2D game engine written in C#. It aims to provide a flexible and extensible platform for rapid prototyping and development of games and interactive applications. The engine is designed with an Entity-Component-System (ECS) architecture and leverages Silk.NET for graphics, input, and windowing.

## Features

- Entity-Component-System (ECS) architecture
- 2D rendering with Silk.NET
- Batch rendering for efficient sprite rendering
- Input handling (keyboard, mouse)
- Sample games (see `Samples/Pong`, `Samples/BatchRenderingExample`)
- Extensible component and system design
- Comprehensive test suite with 45 unit tests

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Compatible OS (Windows, macOS, Linux)

## Build & Run

Clone the repository:

```bash
git clone https://github.com/Anras573/Yaeger.git
cd Yaeger
```

Build the engine and sample projects:

```bash
dotnet build yaeger.sln
```

Run the Pong sample:

```bash
dotnet run --project Samples/Pong/Pong.csproj
```

Run the Batch Rendering example:

```bash
dotnet run --project Samples/BatchRenderingExample/BatchRenderingExample.csproj
```

## Testing

The project includes a comprehensive test suite covering the ECS system and graphics components. To run tests:

```bash
dotnet test
```

For more information about testing, see the [Testing Guide](docs/TESTING.md).

## Project Structure

- `src/Engine/Yaeger/` - Core engine source code
  - `ECS/` - Entity-Component-System framework
  - `Rendering/` - Rendering systems (including batch renderer)
  - `Graphics/` - Graphics primitives and utilities
  - `Input/` - Input handling
  - `Windowing/` - Window management
- `tests/Yaeger.Tests/` - Unit test suite
  - `ECS/` - Tests for ECS components
  - `Graphics/` - Tests for graphics primitives
- `Samples/` - Example games and demos
  - `Pong/` - Classic Pong game implementation
  - `BatchRenderingExample/` - Demonstrates batch rendering for efficient sprite rendering
- `docs/` - Documentation
  - `TESTING.md` - Comprehensive testing guide

## Usage

To create your own game, use the ECS framework provided in `src/Engine/Yaeger/ECS/`. See the Pong sample for a minimal implementation.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or improvements.

## License

This project is licensed under the MIT License. See `LICENSE` for details.

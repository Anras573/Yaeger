# Yaeger

YAEGER - **Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

## Overview

Yaeger is a modular, experimental 2D game engine written in C#. It aims to provide a flexible and extensible platform for rapid prototyping and development of games and interactive applications. The engine is designed with an Entity-Component-System (ECS) architecture and leverages Silk.NET for graphics, input, and windowing.

## Features

- Entity-Component-System (ECS) architecture
- 2D rendering with Silk.NET
- Input handling (keyboard, mouse)
- Sample games (see `Samples/Pong`)
- Extensible component and system design

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

## Project Structure

- `src/Engine/Yaeger/` - Core engine source code
- `Samples/` - Example games and demos

## Usage

To create your own game, use the ECS framework provided in `src/Engine/Yaeger/ECS/`. See the Pong sample for a minimal implementation.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or improvements.

## License

This project is licensed under the MIT License. See `LICENSE` for details.

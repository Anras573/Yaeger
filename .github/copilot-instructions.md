# Yaeger - Copilot Instructions

## Repository Overview

Yaeger is a modular, experimental 2D game engine written in C# using Entity-Component-System (ECS) architecture. The engine leverages Silk.NET for graphics, input handling, and windowing capabilities. This is a small-to-medium sized project with 21 source files across engine and sample code.

**Key Technologies:**
- **Language**: C# with .NET 9.0 target framework
- **Graphics**: Silk.NET (version 2.22.0) for OpenGL rendering
- **Image Processing**: StbImageSharp (version 2.30.15)
- **Architecture**: Entity-Component-System (ECS) pattern
- **Platform**: Cross-platform (Windows, macOS, Linux)

## Build Instructions

### Prerequisites
- **.NET 9.0 SDK** - Critical requirement! The project will NOT build with .NET 8.0 or earlier
- Compatible OS (Windows, macOS, Linux)

### Installing .NET 9.0 (if not available)
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version 9.0.101
export PATH="$HOME/.dotnet:$PATH"
```

### Build Commands (Always run in order)
```bash
# 1. Navigate to repository root
cd /path/to/Yaeger

# 2. Restore dependencies (takes ~6-7 seconds)
dotnet restore yaeger.sln

# 3. Build solution (takes ~5-6 seconds)
dotnet build yaeger.sln
```

### Running the Sample
```bash
# Run the Pong sample game (will fail in headless environments)
dotnet run --project Samples/Pong/Pong.csproj
```

**Note**: The sample requires a display/window system and will throw `System.PlatformNotSupportedException` in headless environments (expected behavior).

### Formatting and Code Quality
The codebase has formatting issues. Before committing changes:
```bash
# Fix code formatting (REQUIRED before commits)
dotnet format

# Verify formatting compliance
dotnet format --verify-no-changes
```

**Known Issues:**
- Many whitespace formatting errors exist in the current codebase
- Files missing final newlines
- Inconsistent indentation in some files
- Always run `dotnet format` before committing changes

## Project Structure

### Solution Organization
- **yaeger.sln** - Main solution file containing 2 projects
- **src/Engine/Yaeger/** - Core engine library (Yaeger.csproj)
- **Samples/Pong/** - Sample Pong game (Pong.csproj)

### Core Engine Architecture (`src/Engine/Yaeger/`)

#### ECS System (`ECS/`)
- **World.cs** - Central ECS world manager, entity lifecycle
- **Entity.cs** - Entity identifier and basic operations
- **ComponentStorage.cs** - Component storage and retrieval
- **WorldExtensions.cs** - Helper methods for world operations

#### Graphics System (`Graphics/`)
- **Sprite.cs** - 2D sprite representation and loading
- **Transform2D.cs** - Position, rotation, scale transformations
- **Camera2D.cs** - 2D camera and view management
- **Color.cs** - Color representation and utilities

#### Rendering System (`Rendering/`)
- **Renderer.cs** - Main rendering pipeline and OpenGL operations
- **Shader.cs** - Shader compilation and management
- **Texture.cs** - Texture loading and binding
- **TextureManager.cs** - Texture resource management
- **Buffer.cs** - OpenGL buffer abstractions
- **VertexArray.cs** - Vertex array object management

#### Input System (`Input/`)
- **Keyboard.cs** - Keyboard input handling via Silk.NET
- **Keys.cs** - Key code definitions and mappings

#### Windowing (`Windowing/`)
- **Window.cs** - Window creation and lifecycle management

#### Systems (`Systems/`)
- **RenderSystem.cs** - ECS rendering system implementation

### Sample Project (`Samples/Pong/`)

#### Game Logic
- **Program.cs** - Main game loop, system setup, and window management
- **EntityFactory.cs** - Entity creation for paddles, ball, background

#### Components (`Components/`)
- **Ball.cs**, **BallState.cs** - Ball entity state management
- **Player.cs**, **PlayerControlled.cs**, **PlayerScore.cs** - Player entities
- **Velocity.cs** - Movement component
- **Bounds.cs** - Collision boundary component

#### Systems (`Systems/`)
- **InputSystem.cs** - Player input processing
- **MoveSystem.cs** - Entity movement updates
- **PhysicsSystem.cs** - Collision detection and response
- **ScoringSystem.cs** - Game scoring logic
- **PrintScoreSystem.cs** - Score display
- **ResetBallSystem.cs** - Ball reset mechanics
- **IUpdateSystem.cs** - System interface definition

#### Assets (`Assets/`)
- **square.png** - Basic white square texture for sprites

## Configuration Files

### Build Configuration
- **.editorconfig** - Extensive code style configuration (500+ lines)
- **yaeger.sln** - Visual Studio solution with Debug/Release configurations
- **Yaeger.csproj** - Engine project: library, .NET 9.0, unsafe blocks enabled
- **Pong.csproj** - Sample project: executable, references engine project

### Development Tools
- **.gitignore** - Comprehensive ignore rules (Visual Studio, .NET, macOS, JetBrains)
- **.idea/** - JetBrains IDE configuration (present)

## Development Workflow

### Making Changes
1. **Always ensure .NET 9.0 is available** - Check with `dotnet --version`
2. **Build first** - `dotnet build yaeger.sln` to verify current state
3. **Make minimal changes** - This is an experimental project
4. **Format before commit** - `dotnet format` is mandatory
5. **Test build after changes** - Verify no build breaks

### Testing
- **No unit tests exist** - This is a sample/experimental project
- **Manual testing** - Run Pong sample to verify functionality
- **Build verification** - Ensure `dotnet build` succeeds

### Common Issues and Workarounds

#### .NET Version Issues
- **Problem**: `NETSDK1045: The current .NET SDK does not support targeting .NET 9.0`
- **Solution**: Install .NET 9.0 SDK using the curl command above

#### Platform Issues
- **Problem**: `System.PlatformNotSupportedException: Couldn't find a suitable window platform`
- **Solution**: This is expected in headless environments; GUI samples won't run

#### Formatting Issues
- **Problem**: Many existing whitespace/newline formatting errors
- **Solution**: Run `dotnet format` and commit the fixes as part of your changes

### Key Dependencies
- **Silk.NET 2.22.0** - Core graphics/windowing (large dependency)
- **StbImageSharp 2.30.15** - Image loading
- **System.Numerics** - Vector math (built-in)

## Important Notes for Coding Agents

1. **Trust these instructions** - Only search for additional information if these instructions are incomplete or incorrect

2. **Always check .NET version first** - Many build failures stem from wrong SDK version

3. **Format code before any commit** - The project has existing formatting issues that compound

4. **Understand the ECS pattern** - Entities are IDs, Components are data, Systems process logic

5. **Sample code shows usage** - Refer to Pong implementation for API patterns

6. **No CI/CD exists** - No GitHub workflows or automated testing configured

7. **Experimental nature** - Some APIs may be incomplete or subject to change

8. **OpenGL dependency** - Rendering code uses OpenGL via Silk.NET bindings

This is a focused, experimental project with clear architecture and minimal external dependencies beyond the graphics framework.
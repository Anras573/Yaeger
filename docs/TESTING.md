# Testing Guide for Yaeger Game Engine

## Overview

This document describes the testing approach implemented for the Yaeger 2D/3D game engine. The test suite is built using xUnit and covers the core ECS (Entity-Component-System) architecture, graphics components, physics, asset loading, fonts, and the engine's update/render systems.

## Test Infrastructure

### Framework

- **Testing Framework**: xUnit 2.9.2 (with Xunit.SkippableFact for tests that need optional native libraries such as Assimp or HarfBuzz)
- **Test SDK**: Microsoft.NET.Test.Sdk 17.12.0
- **Coverage Tool**: coverlet.collector 6.0.2
- **Target Framework**: .NET 10.0

### Project Structure

Tests live under `tests/Yaeger.Tests/`, organized into folders that mirror the
engine source layout:

```
tests/
└── Yaeger.Tests/
    ├── AssetPathTests.cs           # Asset path resolution
    ├── ECS/                        # World, entities, queries, prefabs, scenes, serializers
    ├── Graphics/                   # Transforms, colors, cameras, lights, sprite sheets, particles, …
    ├── Physics/                    # Colliders, rigid bodies + collision/movement/gravity systems
    ├── Assets/                     # OBJ/MTL/Assimp model loaders
    ├── Font/                       # Font manager and SDF glyph atlas
    ├── Rendering/                  # Mesh data, vertex layout, shadow map renderer
    ├── Systems/                    # Parallax, particle, and UI update systems
    ├── Browser/                    # Browser runtime adapters (e.g. time source)
    └── Yaeger.Tests.csproj
```

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Tests with Verbose Output

```bash
dotnet test --verbosity normal
```

### Run Tests in a Specific Project

```bash
dotnet test tests/Yaeger.Tests/Yaeger.Tests.csproj
```

### Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Coverage

The suite has grown well beyond its original ECS/graphics scope — it now contains
**500+ test cases across 50+ files**. The areas below summarize what is covered;
the source files under each folder are the authoritative list.

### ECS System (Entity-Component-System) — `ECS/`

- Entity creation, ID management, and destruction (`WorldTests`, `EntityTests`)
- Component addition, retrieval, removal, and store management
- Two/three/four-component queries and filtering (`WorldExtensionsTests`)
- Prefab loading and building (`PrefabLoaderTests`, `PrefabTests`)
- Scene load/save round-trips (`SceneLoaderTests`, `SceneSaverTests`)
- Component serializers, including 3D and render-layer serializers

### Graphics System — `Graphics/`

- 2D and 3D transforms (`Transform2DTests`, `Transform3DTests`)
- Color construction, alpha, and predefined values (`ColorTests`)
- 2D/3D cameras and frustum culling (`Camera2DTests`, `Camera3DTests`, `CameraFrustumTests`)
- Lights (`DirectionalLightTests`, `PointLightTests`, `SpotLightTests`)
- Materials, sprite sheets, sprite tinting, AABBs, mesh/font handles, particle pools, parallax layers, shadow settings

### Physics — `Physics/`

- Colliders, rigid bodies, velocity, and physics materials (`Components/`)
- Collision detection/resolution, movement, gravity, and the `PhysicsWorld2D` façade (`Systems/`)

### Other areas

- **Assets/** — OBJ/MTL/Assimp model loaders
- **Font/** — font manager and SDF glyph atlas
- **Rendering/** — mesh data, vertex layout, shadow map renderer
- **Systems/** — parallax, particle, and UI update systems
- **Browser/** — browser runtime adapters (e.g. time source)
- **AssetPathTests** — asset path resolution against `AppContext.BaseDirectory`

## Testing Approach

### Unit Testing Philosophy

1. **Isolation**: Tests focus on individual components without external dependencies
2. **Clarity**: Each test has a clear Arrange-Act-Assert structure
3. **Coverage**: Tests cover both happy paths and edge cases
4. **Maintainability**: Tests are organized by namespace matching the source code

### Test Naming Convention

Tests follow the pattern: `MethodOrFeature_ShouldExpectedBehavior`

Examples:
- `CreateEntity_ShouldReturnUniqueEntity`
- `Constructor_ShouldSetPosition`
- `Query_WithTwoComponents_ShouldReturnEntitiesWithBothComponents`

### Test Structure

All tests follow the AAA (Arrange-Act-Assert) pattern:

```csharp
[Fact]
public void MethodName_ShouldDoSomething()
{
    // Arrange - Set up test data and dependencies
    var world = new World();
    var entity = world.CreateEntity();

    // Act - Execute the method under test
    var result = world.TryGetComponent<TestComponent>(entity, out var component);

    // Assert - Verify the expected outcome
    Assert.False(result);
}
```

## What's Tested

### ✅ Covered Areas

1. **ECS Core**
   - Entity lifecycle management
   - Component storage and retrieval
   - World operations
   - Query system for multiple component types

2. **Graphics Primitives**
   - 2D transformations (position, rotation, scale)
   - Transform matrix calculations
   - Color representation and predefined values

3. **Data Structures**
   - Entity as value type
   - Component storage (tested through World API)
   - Collection compatibility

> **Note on ComponentStorage**: `ComponentStorage<T>` is an internal implementation detail of the ECS system. It is not directly tested because users should interact with components exclusively through the `World` class API (`AddComponent`, `RemoveComponent`, `TryGetComponent`, etc.). The component storage behavior is thoroughly tested indirectly through `WorldTests` and `WorldExtensionsTests`.

### ⚠️ Not Tested (Requires Integration Testing)

The following areas are not covered by unit tests as they require platform-specific dependencies or integration testing:

1. **Rendering System**
   - OpenGL operations
   - Shader compilation
   - Texture loading and management
   - Actual rendering output

2. **Input System**
   - Keyboard input (requires Silk.NET's IKeyboard)
   - Input event handling

3. **Windowing**
   - Window creation and management
   - Platform-specific functionality

4. **Audio System**
   - Sound playback
   - Audio resource management

## Adding New Tests

### Creating Tests for a New Component

1. Create a new test file in the appropriate directory:
   ```
   tests/Yaeger.Tests/[Namespace]/[ComponentName]Tests.cs
   ```

2. Use the following template:

```csharp
using Yaeger.[Namespace];

namespace Yaeger.Tests.[Namespace];

public class ComponentNameTests
{
    [Fact]
    public void MethodName_ShouldBehavior()
    {
        // Arrange
        
        // Act
        
        // Assert
    }
}
```

3. Run tests to ensure they pass:
   ```bash
   dotnet test
   ```

### Test Isolation

Each test should be completely independent:
- Create fresh instances for each test
- Don't rely on shared state
- Use helper methods or test components when needed

Example helper component:

```csharp
private struct TestComponent
{
    public int Value;
}
```

## Continuous Integration

### Build and Test Pipeline

The recommended CI pipeline should:

1. Restore dependencies
   ```bash
   dotnet restore yaeger.sln
   ```

2. Build the solution
   ```bash
   dotnet build yaeger.sln --configuration Release
   ```

3. Run tests
   ```bash
   dotnet test yaeger.sln --configuration Release --no-build --verbosity normal
   ```

4. Collect code coverage (optional)
   ```bash
   dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
   ```

### Expected Test Results

All tests should pass. The suite currently holds **500+ test cases across 50+ files**
and runs in a few seconds. Tests that depend on optional native libraries (e.g.
Assimp model loading, HarfBuzz font shaping) use `Xunit.SkippableFact` and skip
automatically when those libraries aren't available, rather than failing.

## Future Testing Improvements

### Recommended Additions

1. **Integration Tests**
   - Test rendering pipeline with mock OpenGL context
   - Test windowing system initialization
   - Test input system with simulated events

2. **Performance Tests**
   - ECS query performance with large entity counts
   - Component storage scalability
   - Memory allocation profiling

3. **Example Tests**
   - Test sample games (Pong, etc.)
   - Ensure examples build and run

4. **Mocking Framework**
   - Consider adding Moq or NSubstitute for interface mocking
   - Mock Silk.NET dependencies for input/windowing tests

## Troubleshooting

### Common Issues

**Issue**: Tests fail with "Cannot bind to the target method"
**Solution**: This indicates a signature mismatch with reflection. Ensure component types are structs and storage methods match expected signatures.

**Issue**: Tests pass locally but fail in CI
**Solution**: Ensure .NET 10.0 SDK is installed in the CI environment.

**Issue**: Tests are slow
**Solution**: Profile tests to identify slow operations. Current test suite runs in under 1 second.

## Best Practices

1. **Keep Tests Fast**: Unit tests should execute in milliseconds
2. **One Assertion Per Test**: Tests should verify one behavior
3. **Test Names Should Be Descriptive**: Anyone should understand what's being tested
4. **Avoid Test Interdependencies**: Tests should run in any order
5. **Use Helper Methods**: Extract common setup into helper methods
6. **Test Edge Cases**: Include null, zero, negative, and boundary values
7. **Keep Tests Maintainable**: Refactor tests as the codebase evolves

## Resources

- [xUnit Documentation](https://xunit.net/)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Test-Driven Development (TDD)](https://en.wikipedia.org/wiki/Test-driven_development)

## Contributing

When adding new features to Yaeger:

1. Write tests first (TDD approach recommended)
2. Ensure all existing tests pass
3. Add tests for new functionality
4. Update this documentation if adding new test categories
5. Run `dotnet csharpier format .` before committing (the project uses CSharpier, not `dotnet format`; a Husky pre-commit hook also formats staged `.cs` files, and CI enforces `dotnet csharpier check .`)

---

**Last Updated**: June 2026
**Test Framework Version**: xUnit 2.9.2
**Test Count**: 500+ tests across 50+ files

using System.Runtime.CompilerServices;

// Lets native-only source files that compile directly into Yaeger (rather than via the
// linked-file glob that shares most of ECS/Graphics/Physics with this assembly — see
// ECS/Serializers/Native/ and Yaeger.csproj) reuse this assembly's internal helpers, e.g.
// ComponentJson/ComponentJson2D, instead of duplicating their logic.
[assembly: InternalsVisibleTo("Yaeger")]

// Lets Yaeger.Tests unit-test the internal pure-math helpers of Core-only systems (e.g.
// PathFollow3DSystem's facing/rotation math) directly, the same way it already does for
// Yaeger-only systems like CameraRigSystem via that assembly's own InternalsVisibleTo.
[assembly: InternalsVisibleTo("Yaeger.Tests")]

# Skeletal Animation

Yaeger plays skeletal (bone/skinning) animations loaded from glTF/FBX files via `AssimpLoader`,
evaluated on the CPU and applied on the GPU through vertex skinning. Static meshes are unaffected —
they simply carry zero skin weights and take the identity-skin path in the shader.

## Pipeline overview

1. **Load** — `AssimpLoader.LoadScene(path)` returns a `ModelScene`. For skinned models it also
   populates `Skeleton` (the bone hierarchy + inverse bind poses) and `Animations`
   (`IReadOnlyList<AnimationClip>`). Each `Vertex3D` carries up to four `BoneIndices`/`BoneWeights`.
2. **Register** — put the skeleton and its clips into a `SkeletonRegistry`, which hands back a
   `SkeletonHandle` (mirrors how `GpuMeshRegistry` works for meshes). Also pass the skinned
   vertices (`IEnumerable<SkinnedVertex>`) if you want frustum culling — see
   [Frustum culling](#frustum-culling) below.
3. **Attach components** — give the mesh entity the `SkeletonHandle` and an `AnimationPlayer`
   (current clip, time, loop, speed) alongside the usual `MeshHandle` / `Transform3D` / `Material3D`.
4. **Update** — `SkeletalAnimationSystem.Update(dt)` advances the player, samples the clip into
   per-bone local transforms, resolves the world-space matrix palette through the hierarchy, and
   writes it to a `BonePalette` component — and, when the skeleton was registered with vertex data,
   an `Aabb3D` bounding the current pose (see [Frustum culling](#frustum-culling)). Call
   `SkeletalAnimationSystem.CrossFadeTo(entity, clip, duration)` instead of assigning
   `AnimationPlayer.CurrentClip` directly to blend into the new clip over `duration` seconds rather
   than popping to it — see [Crossfading](#crossfading) below.
5. **Render** — `MeshRenderSystem` detects the `BonePalette` and routes the entity through
   `Renderer3D`'s skinning draw. A single character (or any group below
   `MeshRenderSystem.InstancingThreshold`) uploads its palette to a bone-matrix uniform buffer (UBO),
   same as before. A crowd of characters sharing `(MeshHandle, Material3D, SkeletonHandle)` at or
   above the threshold instances instead — see [Instanced skinning](instancing.md#instanced-skinning)
   in docs/instancing.md. Either way the vertex shader blends up to four bone matrices per vertex.
   Entities carrying the `Aabb3D` from step 4 are frustum-culled exactly like static meshes — no
   render-path changes needed.

## Types

| Type | Role |
| --- | --- |
| `Bone(Name, ParentIndex, LocalTransform)` | One node in the hierarchy (pre-order; parent index < own). |
| `Skeleton(Bones, InverseBindPoses)` | Bone array + inverse bind poses; `ComputeMatrixPalette` resolves a pose. |
| `VectorKey` / `QuaternionKey` | Keyframes (time in seconds) for translation/scale and rotation. |
| `BoneTrack(BoneIndex, Positions, Rotations, Scales)` | Per-bone keyframe tracks; `Sample(time)` → local matrix. |
| `AnimationClip(Name, Duration, Tracks, Events)` | A named clip; `Sample(time, locals)` fills per-bone locals; `SampleTRS(time, ...)` fills separate translation/rotation/scale spans for blending. `Events` is an optional array of named markers — see [Completion and events](#completion-and-events) below. |
| `SkeletonHandle` / `AnimationPlayer` / `BonePalette` | ECS components. `AnimationPlayer` also holds in-progress crossfade state (`PreviousClip`/`PreviousTime`/`FadeDuration`/`FadeElapsed`), set by `CrossFadeTo` rather than by hand, and `IsFinished` (see below). |
| `SkinnedVertex(Position, BoneIndices, BoneWeights)` | A vertex's bind-pose position + skin influences, passed to `Register` for frustum culling. |
| `SkeletonBoneBounds(BindPositions, Radii)` | Per-bone bind-pose pivot + max influence radius, computed by `Register` from `SkinnedVertex` data. |
| `SkeletonRegistry` | Stores skeletons + clips (+ optional bone bounds), keyed by handle. |
| `SkeletalAnimationSystem` | `IUpdateSystem` that drives playback, writes the palette, and (when bone bounds are available) the per-frame `Aabb3D`. |
| `SkeletalState(ClipName, Loop, FadeDuration)` / `SpeedThreshold(State, MinSpeed)` / `SkeletalAnimationStateMachine` | ECS component (+ value types) for locomotion-driven clip selection — see [below](#locomotion-driven-clip-selection-skeletalanimationstatemachine). |
| `SkeletalAnimationStateMachineSystem` | Switches a `SkeletalAnimationStateMachine` entity's clip via `Play` requests or speed thresholds, driving `SkeletalAnimationSystem.CrossFadeTo`. |

## Usage

```csharp
var modelScene = AssimpLoader.LoadScene("Assets/CesiumMan/CesiumMan.gltf");

var skeletonRegistry = new SkeletonRegistry();
// Passing the skinned vertices lets the registry precompute each bone's max influence radius,
// which SkeletalAnimationSystem uses to write a per-frame Aabb3D for frustum culling.
var skinnedVertices = modelScene.Meshes.SelectMany(
    m => m.Mesh.Vertices.Select(v => new SkinnedVertex(v.Position, v.BoneIndices, v.BoneWeights))
);
var handle = skeletonRegistry.Register(modelScene.Skeleton!, modelScene.Animations, skinnedVertices);
var clip = skeletonRegistry.GetClipNames(handle).FirstOrDefault();

foreach (var mesh in modelScene.Meshes)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, meshRegistry.Register(mesh.Mesh));
    world.AddComponent(entity, Material3D.FromModel(mesh.Material));
    // The skinning palette already places vertices in scene space, so the model matrix is identity.
    world.AddComponent(entity, Transform3D.Identity);
    world.AddComponent(entity, handle);
    world.AddComponent(entity, new AnimationPlayer(clip, loop: true, speed: 1f));
}

var animationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);
window.OnUpdate += dt => animationSystem.Update((float)dt);
window.OnRender += _ => meshRenderSystem.Render();
```

## Crossfading

Assigning `AnimationPlayer.CurrentClip` directly snaps the skeleton to the new clip's pose on the
next `Update` — fine for something like a hit reaction, but a visible pop for everyday transitions
like idle→walk. `CrossFadeTo` blends into the new clip over a short duration instead:

```csharp
animationSystem.CrossFadeTo(entity, "Walk", duration: 0.2f);
```

This captures the entity's current clip and playback time as the fade-out source, then switches
`CurrentClip` to `"Walk"` starting at time zero. While the fade is in progress, `Update` samples
both clips into per-bone translation/rotation/scale (not composed matrices — see
`AnimationClip.SampleTRS`), blends them per bone (`Vector3.Lerp` for translation/scale,
`Quaternion.Slerp` for rotation), and resolves the palette from the blended pose. Both clips keep
advancing their own playback time during the fade — a looping fade-out source doesn't freeze on
whatever pose it happened to be in when the fade began. Once `FadeElapsed` reaches `FadeDuration`,
the next `Update` drops back to the ordinary single-clip path.

A `duration` of zero (or negative, or non-finite) skips the fade entirely: it's the same hard
switch as assigning `CurrentClip` directly, and clears any fade already in progress. Passing
`null` as the clip name fades to the bind pose.

Bones that lack a track in one of the two clips fall back to the skeleton's bind pose
(`Bone.LocalTransform`, decomposed into translation/rotation/scale) for that clip's contribution to
the blend — the same fallback the single-clip path uses for untracked bones.

## Locomotion-driven clip selection: SkeletalAnimationStateMachine

`CrossFadeTo` blends cleanly between clips, but something still has to decide *which* clip should
be playing — without it, a character that stops walking keeps walking on the spot until game code
notices and calls `CrossFadeTo` itself. `SkeletalAnimationStateMachine` +
`SkeletalAnimationStateMachineSystem` is the 3D analogue of `AnimationStateMachine` (see
docs/animation-system.md), sized the same way — "idle / walk / run", not a general animation graph:
no blend trees, no transition-condition DSL.

A "state" maps a name to a `SkeletalState(ClipName, Loop, FadeDuration)` — the clip to play, whether
it loops, and an optional per-state crossfade duration overriding the machine's shared
`DefaultFadeDuration`. Every switch, explicit or automatic, goes through `CrossFadeTo`, so it blends
instead of popping; `SkeletalAnimationStateMachineSystem` also keeps `AnimationPlayer.Loop` in sync
with the target state's own `Loop` flag, since `CrossFadeTo` itself only ever touches the clip and
fade fields.

```csharp
using Yaeger.Graphics;
using Yaeger.Systems;

var states = new Dictionary<string, SkeletalState>
{
    ["idle"] = new SkeletalState("Idle"),
    ["walk"] = new SkeletalState("Walk"),
    ["run"] = new SkeletalState("Run"),
};

var entity = world.CreateEntity();
world.AddComponent(entity, skeletonHandle);
world.AddComponent(entity, new AnimationPlayer("Idle", loop: true));
world.AddComponent(entity, new SkeletalAnimationStateMachine(states, initialState: "idle"));

var animationSystem = new SkeletalAnimationSystem(world, skeletonRegistry);
var stateMachineSystem = new SkeletalAnimationStateMachineSystem(world, animationSystem);

// Game code decides when to switch:
stateMachineSystem.Play(entity, "run");

// Run the state machine system AFTER whatever moves the entity (so a same-frame speed change is
// seen this frame) and BEFORE SkeletalAnimationSystem, so a switch requested this frame is what
// gets sampled this frame rather than one frame later.
window.OnUpdate += deltaTime =>
{
    pathFollowSystem.Update((float)deltaTime);
    stateMachineSystem.Update((float)deltaTime);
    animationSystem.Update((float)deltaTime);
};
```

Re-requesting the state that's already active is a no-op — no crossfade, no restart — unless
`RestartOnReplay` is set to `true` at construction, mirroring `AnimationStateMachine`'s posture.
Requesting an undefined state throws `ArgumentException`, the same as `AnimationStateMachineSystem.Play`.

### Speed-driven selection

Passing `speedThresholds` lets an entity pick its own state without any game code at all, as long as
it also carries a `PathFollow3D` (see docs/path-follow.md): each `Update`, the system reads
`PathFollow3D.CurrentSpeed` and requests whichever threshold's state matches — unless an explicit
`Play` call was already made that same frame, which always wins.

```csharp
SpeedThreshold[] thresholds =
[
    new("idle", MinSpeed: 0f),
    new("walk", MinSpeed: 0.5f),
    new("run", MinSpeed: 3f),
];

world.AddComponent(
    entity,
    new SkeletalAnimationStateMachine(states, "idle", speedThresholds: thresholds, speedHysteresis: 0.2f)
);
```

`speedHysteresis` (units/second) keeps a character hovering right at a threshold from flickering
between the two adjacent states: the active state holds until speed drops below *its own* threshold
minus the hysteresis margin, rather than switching back the moment speed dips under the boundary
that raised it. Moving to a *faster* state is never delayed this way — only downward transitions
need the guard. Without a `PathFollow3D` on the entity, `speedThresholds` has no effect and the
machine only switches via explicit `Play` calls.

## Completion and events

### `AnimationPlayer.IsFinished`

`true` once a non-looping `CurrentClip`'s playback time has clamped at its end (or, for reverse
playback via a negative `Speed`, its start) — the 3D equivalent of `AnimationState.IsFinished` on
the 2D side, so both animation paths behave the same:

```csharp
if (world.GetComponent<AnimationPlayer>(entity).IsFinished)
{
    // the door has finished opening — start the next beat
}
```

Unlike a one-shot event, `IsFinished` is recomputed fresh every `Update` call from the current
`Time`/`Loop`/`Speed` against the clip's duration rather than latched — so it automatically reads
`false` again the moment a new clip is assigned (directly or via `CrossFadeTo`) or `Time` is moved
back before the end (a manual restart). No separate reset call is needed. It's always `false` while
`Loop` is `true`, since a looping clip never clamps.

### `AnimationEventMarker` and `SkeletalAnimationSystem.OnAnimationEvent`

`AnimationClip.Events` is an array of `AnimationEventMarker(Time, Key)` — named moments authored at
specific times within the clip (a footstep, a muzzle flash, a sword-swing whoosh), **sorted
ascending by `Time`**:

```csharp
var walk = new AnimationClip(
    "Walk",
    duration: 0.8f,
    tracks,
    Events: [new AnimationEventMarker(0.2f, "footstepLeft"), new AnimationEventMarker(0.6f, "footstepRight")]
);
```

`SkeletalAnimationSystem.OnAnimationEvent` fires once per marker crossed, carrying
`AnimationEvent(Entity, ClipName, Key)`:

```csharp
animationSystem.OnAnimationEvent += e =>
{
    if (e.Key == "footstepLeft" || e.Key == "footstepRight")
        PlayFootstepSound(e.Entity);
};
```

Markers are evaluated purely from the entity's **incoming** clip (`CurrentClip`/`Time`) — never the
fade-out source of an in-progress `CrossFadeTo`, so a fading-out loop doesn't keep emitting its own
markers once a fade begins. The crossing detection is correct under:

- **Looping wrap-around** — a marker near the clip end fires once per loop pass, including when a
  single large `deltaTime` spans several full loops (each pass fires it again, in order).
- **Reverse playback** — a negative `Speed` fires markers in reverse (descending time) order as
  playback moves backward.
- **A large `deltaTime` spanning several markers** — all of them fire, in the order playback
  actually crosses them, within that one `Update` call.
- **The exact boundary** — a marker sits in a half-open interval per frame (`(previous, current]`
  going forward, `[current, previous)` in reverse), so it fires exactly once as the frame that
  reaches it arrives, never again as the departure end of the next frame.
- **A manual jump of `Time`** — assigning `AnimationPlayer.Time` directly (a seek, bypassing this
  system) fires nothing for the frame the jump is discovered on, even if a marker numerically lies
  between the old and new values. Playback is treated as "continuous" again starting from that
  frame's own advance, so normal crossing detection resumes on the *next* `Update` call. A brand
  new entity's very first `Update` call is **not** treated as a jump — there's no prior frame to be
  discontinuous from, so markers between its initial `Time` and wherever that first frame's advance
  lands fire normally.

Not in scope: importing markers from glTF/FBX (author them in code, or wherever the clip is
registered) and 2D animation events (`AnimationState` already has the completion half; frame-indexed
events there are a separate feature).

## Frustum culling

A bind-pose `Aabb3D` wouldn't bound an animated mesh, so `MeshRenderSystem`'s frustum culling
needs a bound recomputed for the *current* pose every frame — without re-walking every vertex.

When `SkeletonRegistry.Register` is given the skinned vertices (as in [Usage](#usage) above), it
computes, once per bone: the bone's bind-pose pivot (its world position in the rest pose) and the
farthest any vertex with a non-zero weight to that bone sits from that pivot, in bind space (its
max influence radius). This is static per skeleton — it never needs recomputing.

Every `SkeletalAnimationSystem.Update` call then transforms each bone's bind-pose pivot by that
bone's entry in the just-resolved `BonePalette` — giving the bone's *current* animated world
position, since the palette already folds in the inverse bind pose — and expands it by the bone's
precomputed radius. The union of these per-bone boxes is written as the entity's `Aabb3D`, which
`MeshRenderSystem` then culls against exactly like a static mesh's — no render-path changes needed.
Cost is proportional to bone count, not vertex count, and allocates nothing per frame.

This is deliberately conservative rather than tight: linear blend skinning places a vertex at a
convex combination of its influencing bones' transformed positions, and the union of per-bone boxes
is itself convex, so it contains any such combination — a swinging limb never pops in and out of
view at the edge of the screen. A skeleton registered without vertex data (`vertices: null`, the
default) leaves its entities uncullable, same as before this existed.

## Notes & limitations

- **Bone cap** — the immediate (non-instanced) draw path's UBO palette holds up to
  `Renderer3D.MaxBones` (128) matrices. The skeleton indexes every scene node (not just skinning
  joints), so this caps the total node count for that path. If a vertex references a bone index
  outside `[0, 128)`, the shader safely falls back to identity skin (bind pose) for that vertex
  rather than reading out of bounds — so over-cap models degrade gracefully rather than crashing.
  Models within typical joint counts (the CesiumMan sample has 22) are unaffected. The instanced
  skinning path (see docs/instancing.md#instanced-skinning) isn't bound by this 128 cap — its
  texture-buffer palette scales with the group's total bone count instead — but is itself capped
  per draw call by `InstancedSkinningPlanner.MaxPaletteTexels` (16384 bones), split into more calls
  rather than dropped past that.
- **Influences** — up to four bones per vertex; the loader keeps the heaviest four and renormalises.
- **Model matrix** — for skinned entities use `Transform3D.Identity`; the bone world transforms run
  from the scene root, so the skin already positions vertices in scene space. This assumes the
  source file's own mesh node has an identity transform, true for every glTF-sourced model this
  pipeline ships with (`CesiumMan`, `DamagedHelmet`) — `AssimpLoader.LoadScene` bakes a skinned
  mesh's own (non-identity) node transform into its vertex data at load time so this holds for other
  formats too, an FBX whose mesh node carries its own scale/rotation (a common Blender export
  artifact) included — see the Knight character in `Samples/SponzaNight`, and the code comment on
  `AssimpLoader.ExtractMeshData`, for the asset that exposed the gap this closes. A non-identity
  `Transform3D` on a skinned entity (as `SponzaNight`'s knight uses, to size a character whose
  armature *object* — not its mesh node — separately carries its own baked scale) is layered on top
  of the skin as usual, the same as any static mesh's.
- **Culling assumes rigid-ish bones** — the per-bone radius is a bind-space distance, carried
  through the palette unchanged; it stays exact under rotation/translation (the common case) but a
  clip that scales a bone non-uniformly could in principle stretch a vertex further than the radius
  accounts for. Not a concern for typical rig/clip authoring, and out of scope for now — see
  [Frustum culling](#frustum-culling).

See [`Samples/SkinnedMeshDemo`](../Samples/SkinnedMeshDemo) for a complete example that plays the
KhronosGroup CesiumMan walk cycle, and
[`Samples/CrowdDemo`](../Samples/CrowdDemo) for many characters sharing one skeleton drawing through
the instanced skinning path (see docs/instancing.md#instanced-skinning).

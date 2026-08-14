# Skeletal Animation

Yaeger plays skeletal (bone/skinning) animations loaded from glTF/FBX files via `AssimpLoader`,
evaluated on the CPU and applied on the GPU through vertex skinning. Static meshes are unaffected —
they simply carry zero skin weights and take the identity-skin path in the shader.

## Pipeline overview

1. **Load** — `AssimpLoader.LoadScene(path)` returns a `ModelScene`. For skinned models it also
   populates `Skeleton` (the bone hierarchy + inverse bind poses) and `Animations`
   (`IReadOnlyList<AnimationClip>`). Each `Vertex3D` carries up to four `BoneIndices`/`BoneWeights`.
2. **Register** — put the skeleton and its clips into a `SkeletonRegistry`, which hands back a
   `SkeletonHandle` (mirrors how `GpuMeshRegistry` works for meshes).
3. **Attach components** — give the mesh entity the `SkeletonHandle` and an `AnimationPlayer`
   (current clip, time, loop, speed) alongside the usual `MeshHandle` / `Transform3D` / `Material3D`.
4. **Update** — `SkeletalAnimationSystem.Update(dt)` advances the player, samples the clip into
   per-bone local transforms, resolves the world-space matrix palette through the hierarchy, and
   writes it to a `BonePalette` component. Call `SkeletalAnimationSystem.CrossFadeTo(entity, clip,
   duration)` instead of assigning `AnimationPlayer.CurrentClip` directly to blend into the new
   clip over `duration` seconds rather than popping to it — see [Crossfading](#crossfading) below.
5. **Render** — `MeshRenderSystem` detects the `BonePalette` and routes the entity through
   `Renderer3D`'s skinning draw, uploading the palette to a bone-matrix uniform buffer (UBO). The
   vertex shader blends up to four bone matrices per vertex.

## Types

| Type | Role |
| --- | --- |
| `Bone(Name, ParentIndex, LocalTransform)` | One node in the hierarchy (pre-order; parent index < own). |
| `Skeleton(Bones, InverseBindPoses)` | Bone array + inverse bind poses; `ComputeMatrixPalette` resolves a pose. |
| `VectorKey` / `QuaternionKey` | Keyframes (time in seconds) for translation/scale and rotation. |
| `BoneTrack(BoneIndex, Positions, Rotations, Scales)` | Per-bone keyframe tracks; `Sample(time)` → local matrix. |
| `AnimationClip(Name, Duration, Tracks, Events)` | A named clip; `Sample(time, locals)` fills per-bone locals; `SampleTRS(time, ...)` fills separate translation/rotation/scale spans for blending. `Events` is an optional array of named markers — see [Completion and events](#completion-and-events) below. |
| `SkeletonHandle` / `AnimationPlayer` / `BonePalette` | ECS components. `AnimationPlayer` also holds in-progress crossfade state (`PreviousClip`/`PreviousTime`/`FadeDuration`/`FadeElapsed`), set by `CrossFadeTo` rather than by hand, and `IsFinished` (see below). |
| `SkeletonRegistry` | Stores skeletons + clips, keyed by handle. |
| `SkeletalAnimationSystem` | `IUpdateSystem` that drives playback and writes the palette. |

## Usage

```csharp
var modelScene = AssimpLoader.LoadScene("Assets/CesiumMan/CesiumMan.gltf");

var skeletonRegistry = new SkeletonRegistry();
var handle = skeletonRegistry.Register(modelScene.Skeleton!, modelScene.Animations);
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

## Notes & limitations

- **Bone cap** — the shader palette holds up to `Renderer3D.MaxBones` (128) matrices. The skeleton
  indexes every scene node (not just skinning joints), so this caps the total node count. If a vertex
  references a bone index outside `[0, 128)`, the shader safely falls back to identity skin (bind
  pose) for that vertex rather than reading out of bounds — so over-cap models degrade gracefully
  rather than crashing. Models within typical joint counts (the CesiumMan sample has 22) are unaffected.
- **Influences** — up to four bones per vertex; the loader keeps the heaviest four and renormalises.
- **Shadows** — the shadow pass renders the bind pose (it samples only positions), so skinned meshes
  are not yet animated in shadow maps. Avoid combining skinned meshes with the shadow pass for now.
- **Model matrix** — for skinned entities use `Transform3D.Identity`; the bone world transforms run
  from the scene root, so the skin already positions vertices in scene space.

See [`Samples/SkinnedMeshDemo`](../Samples/SkinnedMeshDemo) for a complete example that plays the
KhronosGroup CesiumMan walk cycle.

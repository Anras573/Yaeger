# Instanced Rendering

`MeshRenderSystem` draws many entities that share the same mesh and material in a single
`glDrawElementsInstanced` call instead of one `glDrawElements` call per entity. This is **automatic**
and requires no opt-in: any scene with several entities carrying the same `MeshHandle` +
`Material3D` benefits without code changes, and a scene with only unique meshes/materials renders
exactly as it did before this feature existed.

## How it works

Each frame, after frustum culling, `MeshRenderSystem` groups surviving non-skinned entities by
`(MeshHandle, Material3D)` using `MeshInstanceBatcher` — pure CPU-side bookkeeping, no GL calls. A
group at or above `MeshRenderSystem.InstancingThreshold` (default `4`) is drawn via
`Renderer3D.DrawInstanced`; smaller groups fall back to the immediate per-entity
`Renderer3D.Draw` loop, since the extra instance-buffer upload isn't worth it for a handful of
entities.

The shadow depth pre-pass (`ShadowMapRenderer`) is grouped the same way, but **by mesh alone** —
depth-only rendering never reads material state, so casters that share a mesh collapse into one
instanced draw even if their real materials (used by the main pass) differ.

Skinned entities (carrying `BonePalette`) that also carry a `SkeletonHandle` are grouped and
instanced too — see [Instanced skinning](#instanced-skinning) below. A skinned entity without a
`SkeletonHandle` (unusual, but not invalid) falls back to the immediate per-entity skinning draw,
since there's no skeleton to group it by.

## GPU side

Per-instance data — a model matrix plus its pre-computed normal matrix (the model's inverse-
transpose, computed on the CPU exactly like the non-instanced path does) — is streamed into a
per-`GpuMesh` instance buffer (`InstanceData`, grown by doubling, never shrunk). Vertex attributes
6-9 (model, one `vec4` column each) and 10-12 (normal matrix, one `vec3` column each) read that
buffer with `VertexAttribDivisor` 1, so they advance once per instance instead of once per vertex.

`Renderer3D.vert` and `ShadowMap.vert` both gate their instanced path behind a `uInstanced` uniform,
the same pattern GPU skinning already uses for `uSkinned`: when `uInstanced == 0` the shader reads
the usual `uModel`/`uNormalMatrix` uniforms, so the non-instanced path is bit-for-bit unchanged.
Rendered output is identical between the two paths — same lighting, same shadows — because both
compute the same model/normal matrices, just via a uniform upload vs. a per-instance attribute read.

## Instanced skinning

A crowd of characters sharing a `(MeshHandle, Material3D, SkeletonHandle)` — same mesh, same
material, same skeleton, but each playing its own pose — draws in one or a handful of instanced
calls instead of one per character. The fixed 128-matrix bone UBO the immediate skinned draw path
uses (`Renderer3D.MaxBones`) can only ever hold *one* character's palette at a time, so instancing a
crowd needs a different bone-data path:

- `MeshInstanceBatcher.AddSkinned` groups by `(MeshHandle, Material3D, SkeletonHandle)` — a separate
  axis from the static `(MeshHandle, Material3D)` grouping `Add` uses, since two skinned groups
  sharing a mesh/material but driven by different skeletons (different bone counts/hierarchies)
  can't share one instanced draw's palette data. Each group also carries, parallel to `Models`, that
  instance's own resolved `BonePalette.Matrices` (`BonePalettes`) and its cumulative starting
  bone-slot offset into the group's palettes packed back-to-back (`PaletteOffsets`) — pure CPU-side
  bookkeeping, unit-tested like the rest of `MeshInstanceBatcher`.
- Every instance's palette is packed (`BonePaletteBuffer`, also pure CPU-side and unit-tested) into
  one flat buffer of `Vector4` texels — 4 texels (its columns, raw-memory order) per bone matrix —
  and uploaded to a `GL_TEXTURE_BUFFER` sampled in the vertex shader as `uBonePalette`, a
  `samplerBuffer` read with `texelFetch`. A new per-instance vertex attribute (location 13,
  `aInstancePaletteBase`, `VertexAttribDivisor` 1) tells each instance where its own palette starts
  in that buffer, mirroring how the model-matrix instance attributes already work.
- `uSkinned` and `uInstanced` compose: `uSkinned != 0 && uInstanced != 0` reads bone matrices from
  `uBonePalette` via `aInstancePaletteBase`; `uSkinned != 0 && uInstanced == 0` reads the fixed UBO
  as before; `uSkinned == 0` skips skinning entirely regardless of `uInstanced`. A single skinned
  character (below `InstancingThreshold`) renders through the unchanged immediate/UBO path.

### Palette size cap

A texture buffer's capacity is finite (`InstancedSkinningPlanner.MaxPaletteTexels`, 65536 texels —
the `GL_MAX_TEXTURE_BUFFER_SIZE` every OpenGL 3.1+ implementation is guaranteed to support at
minimum). At 4 texels per bone, that's **16384 bones per instanced-skinned draw call** — e.g. 256
fully-rigged 64-bone characters, or thousands of simpler ones. A group whose combined palettes would
exceed that is never silently truncated: `InstancedSkinningPlanner.PlanChunks` (pure CPU-side,
unit-tested) splits it into as many chunks as needed, each drawn with its own
`Renderer3D.DrawInstancedSkinned`/`ShadowMapRenderer.DrawInstancedSkinned` call — every instance is
still drawn, just via more than one call once a single group is that large.

See `Samples/Benchmarks` (its `crowd` benchmark) for a live demo: many CesiumMan characters sharing
one skeleton, each playing the walk clip out of phase, printing the resulting draw-call count once
per second.

## Measuring it

`Renderer3D.DrawCallCount` and `ShadowMapRenderer.DrawCallCount` count real GL draw calls issued
since the last `BeginFrame3D`/`BeginPass` — an instanced group of any size counts as **one** (or, for
a skinned group past the palette size cap above, one per chunk). Read them after
`MeshRenderSystem.Render()` to verify a scene collapsed to the expected number of calls.

See `Samples/Benchmarks` (its `instancing` benchmark) for a live demo: a large grid of boxes
sharing two materials, printing FPS and both draw-call counts once per second. Press **I** to force
`InstancingThreshold` to `int.MaxValue` (disabling instancing) and compare against the default.

## Tuning / limitations

- `MeshRenderSystem.InstancingThreshold` is a public, mutable `int` — lower it to instance smaller
  groups, or raise it (e.g. to `int.MaxValue`) to disable instancing entirely for comparison. It
  applies to skinned groups the same way it does to static ones.
- No per-instance material variation (tint, texture) beyond the transform — out of scope for v1,
  same as the GitHub issue that introduced this feature.
- No GPU culling or indirect draws — CPU-side frustum culling (against `Aabb3D`) still runs per
  entity before grouping, same as the pre-instancing path.
- No pose cache — entities playing the same clip at the same phase each still get their own CPU
  animation sample and their own slot in the palette buffer; deduplicating identical poses is a
  possible future optimization, not implemented here.
- Instanced skinning is directional-shadow and main-pass only. Point-light shadow casters
  (`PointShadowMapRenderer`) always take the immediate per-entity skinning path regardless of
  `SkeletonHandle`/group size — out of scope for now, consistent with point/spot shadows being out
  of scope for GPU skinning generally (see docs/shadows.md).

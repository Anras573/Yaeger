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

Skinned entities (carrying `BonePalette`) always take the existing per-entity draw path in both
passes: a bone palette is per-entity state and can't be folded into a per-instance attribute.

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

## Measuring it

`Renderer3D.DrawCallCount` and `ShadowMapRenderer.DrawCallCount` count real GL draw calls issued
since the last `BeginFrame3D`/`BeginPass` — an instanced group of any size counts as **one**. Read
them after `MeshRenderSystem.Render()` to verify a scene collapsed to the expected number of calls.

See `Samples/MeshInstancingDemo` for a live demo: a large grid of boxes sharing two materials,
printing FPS and both draw-call counts once per second. Press **I** to force
`InstancingThreshold` to `int.MaxValue` (disabling instancing) and compare against the default.

## Tuning / limitations

- `MeshRenderSystem.InstancingThreshold` is a public, mutable `int` — lower it to instance smaller
  groups, or raise it (e.g. to `int.MaxValue`) to disable instancing entirely for comparison.
- No per-instance material variation (tint, texture) beyond the transform — out of scope for v1,
  same as the GitHub issue that introduced this feature.
- No GPU culling or indirect draws — CPU-side frustum culling (against `Aabb3D`) still runs per
  entity before grouping, same as the pre-instancing path.

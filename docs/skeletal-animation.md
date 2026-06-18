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
   writes it to a `BonePalette` component.
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
| `AnimationClip(Name, Duration, Tracks)` | A named clip; `Sample(time, locals)` fills per-bone locals. |
| `SkeletonHandle` / `AnimationPlayer` / `BonePalette` | ECS components. |
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

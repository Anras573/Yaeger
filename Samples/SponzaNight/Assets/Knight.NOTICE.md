# Knight Character — asset notice

Fetched automatically at build time by the `FetchKnightAssets` target in `SponzaNight.csproj` (see
`Assets/Knight/` — not committed, gitignored like the other fetched samples). This file is the
committed record of where it came from and under what terms, per `Assets/Knight/` not being
tracked in git. Identical to `Samples/Sponza/Assets/Knight.NOTICE.md` — this sample fetches its own
independent copy rather than sharing `Samples/Sponza`'s, the same "each sample is independently
buildable" convention `SkinnedMeshDemo`/`Benchmarks` (its crowd benchmark) follow for their own
copies of CesiumMan.

- **Asset**: "Knight Character" (lowpoly rigged humanoid with idle/walk/run/roll/death/attack
  animations)
- **Author**: Quaternius (<https://www.patreon.com/quaternius>)
- **Source**: <https://opengameart.org/content/lowpoly-animated-knight>
- **Download**: <https://opengameart.org/sites/default/files/Knight%20Character%20by%20%40Quaternius.zip>
- **Licence**: CC0 1.0 Universal (public domain) — no attribution required. This notice is kept
  anyway so the source and terms are on record.
- **Pinned SHA256**: `36aed14d0e27282bd006982e00206a852b1d8c00fc72f95c2437d9b4b5dccdb9`
  (of the zip archive; verified by `FetchKnightAssets` before extraction)

Only `FBX/KnightCharacter.fbx` is fetched out of the archive — the rigged body mesh plus its
animation clips. The `.fbx` embeds its own material colours (no external textures), and the
accompanying weapon/helmet/OBJ/Blender files in the archive aren't needed for a skinned-character
sample, so they're left unfetched.

## Import verification (`AssimpLoader`)

Confirmed by loading `KnightCharacter.fbx` through `Yaeger.Assets.AssimpLoader.LoadScene`:

- Skeleton: 45 bones — well within `Renderer3D.MaxBones` (128)
- 12 named animation clips, including stable idle/walk names to reference from a state machine:
  - `HumanArmature|Idle`
  - `HumanArmature|Walking`
  - (plus `Run`, `Jump`, `Roll`, `Death`, and sword-variant clips not used here)
- 3 meshes / 3 materials (Armor, Boots, Skin), no external texture references

## Baked scale, and the `AssimpLoader` fix it exposed

The FBX's `HumanArmature` node (the armature's own object transform, one level above the actual
skeleton root) carries a baked scale of 100 in each axis — a common Blender "forgot to apply scale
before export" artifact — and the mesh's own node carries a matching, separately-baked scale of 100
plus a -90 degree rotation about X (a Z-up to Y-up correction).

`AssimpLoader` previously discarded a skinned mesh's own node transform entirely: the documented
convention (`docs/skeletal-animation.md`, "for skinned entities use `Transform3D.Identity`") is
that the skin already positions vertices in scene space, which is only true when that node's
transform is identity - the case for every skinned model this pipeline had been exercised against
so far (`CesiumMan`, glTF-sourced with a frozen identity transform on its mesh node). This FBX's
non-identity mesh-node transform exposed the gap: the bone offset matrices Assimp computes are
relative to that transform, so skinning without it folds a skinned character into the wrong space
entirely - confirmed via the shipped `SkeletalAnimationSystem`'s own resolved `Aabb3D`, which came
out roughly cubic (a scrambled silhouette) rather than tall-and-narrow. Fixed in
`AssimpLoader.ExtractMeshData`: a skinned mesh's own node transform is now baked into its vertex
positions/normals/tangents at load time, mirroring what a static mesh gets via `ModelMesh.Transform`
at render time (see the code comment there, and
`AssimpLoaderTests.LoadScene_SkinnedMeshWithNonIdentityNodeTransform_ShouldBakeTransformIntoVertices`
for the regression test). That fix corrects the mesh-node half of the scale mismatch; `Program.cs`'s
`KnightScale` constant compensates for the remaining armature-side scale, sized from the post-fix
`Aabb3D` (~560 units tall) against a ~1.8m target.

Confirmed by rendering the sample (a screenshot captured via `YAEGER_SCREENSHOT` under Xvfb): the
knight comes out a clearly humanoid, plausibly human-scale figure - head, torso, legs, and boots all
proportioned sensibly next to nearby architecture.

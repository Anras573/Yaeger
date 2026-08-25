# Knight Character — asset notice

Fetched automatically at build time by the `FetchKnightAssets` target in `Sponza.csproj` (see
`Assets/Knight/` — not committed, gitignored like the other fetched samples). This file is the
committed record of where it came from and under what terms, per `Assets/Knight/` not being
tracked in git.

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

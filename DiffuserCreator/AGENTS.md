# AGENTS.md — DiffuserCreator Development Guide

> This file is the architecture overview. Task-specific deep dives — the generation pipeline, the hand-built block mesh, editor workflows, and build/serialization traps — live in the skill library. Start at [.claude/skills/README.md](.claude/skills/README.md) for the routing table. Each skill ends with re-verification commands; if code and skill disagree, the code wins — update the skill in the same change.

## What this is

DiffuserCreator is a **Unity 2022.1.15f1 editor tool** that generates the blueprint for an acoustic diffuser: a wall panel of many small cube "blocks" whose front faces are carved to varying depths. The depth pattern scatters reflected sound instead of echoing it, and the arrangement is meant to be visually pleasing. The generated geometry is exported (OBJ / mesh `.asset` / FBX) to drive a physical build.

It is a rough 2021–2022 prototype being resurrected. There is no CI and no test project; verification is manual in the editor (see the `diffuser-build-and-run` skill).

## Architecture overview

```
DiffuserGrid (MonoBehaviour)          Assets/Scripts/DiffuserGrid.cs
  └─ spawns _rows × _columns of ↓
DiffuserBlock (MonoBehaviour)         Assets/Scripts/DiffuserBlock.cs
  └─ builds its OWN mesh: a cube whose 4 front-face corners have independent depths
  └─ depth source chosen by HeightEditing: Cutting | Curve | Custom
DiffuserBlockSequence (plain C#)      Assets/Scripts/DiffuserBlockSequence.cs
  └─ a row or column of blocks sharing one AnimationCurve
```

The core loop: `DiffuserGrid.Generate()` (run on `Start` or via a `[ContextMenu]`) instantiates the block prefab per grid cell, sizes it via `transform.localScale`, and calls `block.Initialize(...)`. Each block computes its four corner depths from either a **cutting surface** (raycast against a collider), an **AnimationCurve** (evaluated at the block's normalized grid position, in `Height` or `Angle` mode), or **manual** values, then builds a 24-vertex box mesh and matching collider.

A separate **runtime selection layer** (`SelectionManager` + `SelectableBlock` + the third-party `RuntimeTransformHandle` plugin) lets you hover/pick blocks and move them with a gizmo. `VertexIndicator` labels corner indices; `CameraLookAt` orbits.

## Project structure

```
Assets/Scripts/              # All first-party gameplay code (9 files, currently global namespace)
Assets/Scenes/MainScene.unity   # The scene; DiffuserGrid regenerates on Play
Assets/Prefabs/              # DiffuserGrid, DiffuserBlock, CuttingSurface, VertexIndicator
Assets/Materials/            # Hover / Selected / Vertex / CuttingPlane materials
Assets/GeneratedMesh/, Assets/Meshes/   # Export outputs (OBJ / FBX / .asset)
Assets/Plugins/RuntimeTransformHandle/  # Third-party gizmo plugin (namespace RuntimeHandle) — DO NOT refactor
Assets/UI/, Assets/MainMenu.uxml         # UI Toolkit (unfinished)
PDFsharp/                    # Vendored PDF library at repo root — NOT compiled by Unity, NOT wired in
Packages/manifest.json       # ProBuilder, FBX exporter, TextMeshPro, UI Builder, Timeline, …
```

## Key components

| Component | Role |
|---|---|
| `DiffuserGrid` | Owns grid dimensions, spacing, block size, curve config, snap angle; generates blocks and per-row/column sequences; hosts all `[ContextMenu]` actions and mesh export. |
| `DiffuserBlock` | One cell. Owns 8 corner points, 4 corner depths, its mesh + collider. Implements the three depth sources and the two curve modes. |
| `DiffuserBlockSequence` | Plain C# row/column wrapper; pushes a shared curve to its blocks via `SetCurve`. |
| `CuttingSurface` | Empty marker `MonoBehaviour`; its collider is the shape blocks carve against in `Cutting` mode. |
| `SelectionManager` / `SelectableBlock` | Runtime hover/select + `RuntimeTransformHandle` gizmo. |
| `VertexIndicator` | TextMeshPro label of a corner index. |
| `CameraLookAt` | Keeps a camera aimed at a target. |
| `ObjExporterScript` / `ObjExporter` | Wavefront OBJ export (editor-only). |

## Two traps that bite hardest

1. **Editor-only APIs in runtime scripts.** `Assets/Scripts/` has no asmdef, so everything compiles into `Assembly-CSharp`. `ObjExporterScript` and `DiffuserGrid.SaveMesh` use `UnityEditor`/`AssetDatabase` — fine in the editor, **won't compile in a player build**. Guard with `#if UNITY_EDITOR` or move to an `Editor/` asmdef.
2. **Serialized-field renames lose data.** Grid/block config is stored in prefabs and the scene by field name. Renaming a `public`/`[SerializeField]` field silently resets it. Use `[FormerlySerializedAs("oldName")]` for any rename — including fixing the "Dioganal" misspelling. Never rename the `MonoBehaviour` class or `.cs` file.

Both are detailed in the `diffuser-build-and-run` skill.

## Development environment

- Unity **2022.1.15f1** (exact; non-LTS). Open, let it recompile, watch the Console.
- ProBuilder and TextMesh Pro are **code** dependencies (not just tools) — don't remove them.
- Do not hand-edit or commit the generated `.csproj`/`.sln` files, or `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`.

## Known issues (as of resurrection)

- `DiffuserBlock.SetCurve` disables the vertical curve it was just given (`_useVerticalCurve = false`) — a bug.
- Debug logging uses `Debug.LogError` for normal info (`Angle`, `Magnitude`, `PrintGrid`) — noisy.
- `LineLineIntersection`'s return value is ignored in angle mode, so a non-intersection yields a bogus depth.
- Empty `Start()`/`Update()`/`OnDrawGizmos()` bodies in several scripts.
- `CuttingSurface` is an empty component.
- No namespaces; editor-only code unguarded (Trap 1).

## Code style

See [CLAUDE.md](CLAUDE.md) for the code-style rules (adopted from the WeAre VR project) and conventions.

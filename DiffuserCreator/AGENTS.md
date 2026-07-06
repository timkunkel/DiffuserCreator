# AGENTS.md — DiffuserCreator Development Guide

> This file is the architecture overview. Task-specific deep dives — the generation/shaping pipeline, the hand-built block mesh, editor workflows + the runtime UI, and build/serialization traps — live in the skill library. Start at [.claude/skills/README.md](.claude/skills/README.md) for the routing table. Each skill ends with re-verification commands; if code and skill disagree, the code wins — update the skill in the same change.

## What this is

DiffuserCreator is a **Unity 6000.3.9f1 editor tool** (upgraded from 2022.1.15f1 during resurrection) that generates the blueprint for an acoustic diffuser: a wall panel of many small cube "blocks" whose front faces are carved to varying depths. The depth pattern scatters reflected sound instead of echoing it, and the arrangement is meant to be visually pleasing. The generated geometry is exported (OBJ / mesh `.asset` / FBX) to drive a physical build.

There is no CI and no test project; verify manually in the editor, or compile-check offline with Roslyn (see the `diffuser-build-and-run` skill).

## Architecture overview

The code is split into layers so that geometry, depth behavior, configuration data, and orchestration are separate concerns:

```
DiffuserGrid (orchestrator)     Assets/Scripts/DiffuserGrid.cs
  ├─ owns config, builds a DiffuserSettings snapshot each rebuild
  ├─ spawns _rows × _columns of DiffuserBlock and lays them out
  └─ picks a DepthShaper by DepthSource, applies it to every block
DepthShaper (behavior)          Assets/Scripts/DepthShaper.cs
  ├─ CuttingDepthShaper  → raycast toward -Z against a cutting-layer collider
  ├─ CurveDepthShaper    → evaluate AnimationCurves (Height or Angle mode)
  └─ ManualDepthShaper   → leave depths flat
DiffuserBlock (geometry)        Assets/Scripts/DiffuserBlock.cs
  └─ a dumb cube: size + 4 corner depths + mesh + collider + indicators
DiffuserSettings (data)         Assets/Scripts/DiffuserSettings.cs
  └─ enums + per-rebuild snapshot of the shaping parameters
DiffuserControlPanel (UI)       Assets/Scripts/UI/DiffuserControlPanel.cs
  └─ runtime UI Toolkit panel binding every setting to Generate/Reshape
```

Core loop: `DiffuserGrid.Generate()` (on `Start` or via `[ContextMenu]`/UI) instantiates the block prefab per cell, sizes it via `transform.localScale`, sets its normalized grid position, then `Reshape()` builds a `DiffuserSettings` and delegates depth to the active `DepthShaper`. Structural changes call `Generate()`; depth/curve changes call the cheaper `Reshape()`.

The previous god-object `DiffuserBlock` (which held cutting, curve, and grid logic) is now pure geometry — **all depth decisions live in `DepthShaper`**. Adding a new way to drive depth = subclass `DepthShaper` + one factory case; the block is untouched.

A separate **runtime selection layer** (`SelectionManager` + `SelectableBlock` + the vendored `RuntimeTransformHandle` plugin) hovers/picks blocks and moves them with a gizmo. `VertexIndicator` labels corners; `CameraLookAt` orbits.

## Project structure

```
Assets/Scripts/              # First-party code (DiffuserCreator namespace)
Assets/Scripts/UI/           # DiffuserControlPanel (DiffuserCreator.UI)
Assets/Scripts/Editor/       # DiffuserControlPanelSetup menu (DiffuserCreator.EditorTools, editor-only)
Assets/Scenes/MainScene.unity   # The scene; DiffuserGrid regenerates on Play
Assets/Prefabs/              # DiffuserGrid, DiffuserBlock, CuttingSurface, VertexIndicator
Assets/Materials/            # Hover / Selected / Vertex / CuttingPlane materials
Assets/GeneratedMesh/, Assets/Meshes/   # Export outputs (OBJ / FBX / .asset)
Assets/Plugins/RuntimeTransformHandle/  # Third-party gizmo plugin (namespace RuntimeHandle) — DO NOT refactor
Assets/UI/                   # PanelSettings lives here after control-panel setup
PDFsharp/                    # Vendored PDF library at repo root — NOT compiled by Unity, NOT wired in
Packages/manifest.json       # ProBuilder, FBX exporter, TextMeshPro, UI Builder, Timeline, …
```

## Key components

| Component | Role |
|---|---|
| `DiffuserGrid` | Owns config; generates blocks + lays them out; builds `DiffuserSettings`; applies a `DepthShaper`; hosts `[ContextMenu]`s and mesh export; exposes get/set properties for the UI. |
| `DepthShaper` (+ `Cutting`/`Curve`/`Manual`) | Strategy that computes a block's four corner depths from a `DiffuserSettings`. |
| `DiffuserBlock` | Dumb cube: 8 points, 4 corner depths, mesh + collider, vertex indicators. `SetSize`/`SetUniformDepth`/`SetDepths`/`NormalizedPosition`/`Angle`. |
| `DiffuserSettings` | `[Serializable]` shaping snapshot + the `HeightMode`/`DepthSource`/`CurveMode` enums. |
| `GeometryUtils` | Static `LineLineIntersection` (used by Angle-mode tilt). |
| `DiffuserControlPanel` | Runtime UI Toolkit panel (needs a `UIDocument` + `PanelSettings`). |
| `DiffuserControlPanelSetup` | Editor menu **Tools ▸ DiffuserCreator ▸ Create Control Panel** to wire the panel. |
| `CuttingSurface` | Empty marker; its collider is the shape blocks carve against in `Cutting` mode. |
| `SelectionManager` / `SelectableBlock` | Runtime hover/select + `RuntimeTransformHandle` gizmo. |
| `VertexIndicator` / `CameraLookAt` | Corner label / camera aim. |
| `ObjExporterScript` / `ObjExporter` | Wavefront OBJ export (editor-only). |

## Two traps that bite hardest

1. **Editor-only APIs in runtime scripts.** `Assets/Scripts/` compiles into `Assembly-CSharp`; `UnityEditor` is stripped from player builds. The editor-only code (`ObjExporterScript`, `DiffuserGrid.SaveAsMesh`, `Editor/DiffuserControlPanelSetup`) is isolated behind `#if UNITY_EDITOR` and/or the `Editor/` folder — keep it that way.
2. **Serialized-field renames lose data.** Grid config lives in the prefab and as **scene overrides** in `MainScene.unity`, keyed by field name. Renaming — or moving into a nested object — silently resets values. Use `[FormerlySerializedAs("oldName")]`; that's why the grid's settings stay flat and `DiffuserSettings` is only a runtime snapshot. Never rename the `MonoBehaviour` class or `.cs` file.

Both are detailed in `diffuser-build-and-run`.

## Development environment

- Unity **6000.3.9f1**. Open, recompile, watch the Console — or compile-check offline (Roslyn recipe in `diffuser-build-and-run`).
- ProBuilder, TextMesh Pro, and UI Toolkit are **code** dependencies — don't remove them.
- Do not hand-edit or commit generated `.csproj`/`.sln`, or `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`.

## Status after resurrection refactor

Fixed and restructured: depth logic extracted from `DiffuserBlock` into `DepthShaper` strategies; settings centralized into `DiffuserSettings`; the vertical-curve enable bug, the ignored intersection result, and the `LogError`-as-info misuse are resolved; "Dioganal" renamed with `[FormerlySerializedAs]`; editor-only code guarded; a runtime UI Toolkit control panel added; `DiffuserBlockSequence` (dead) removed. All scripts compile clean against Unity 6000.3.9f1.

Possible next steps: an `Editor/` asmdef to formalize the editor/runtime split; per-block depth overrides via a `Manual` editing path in the UI; a PDF blueprint export (PDFsharp is vendored but unwired).

## Code style

See [CLAUDE.md](CLAUDE.md) for the code-style rules (adopted from the WeAre VR project) and conventions.

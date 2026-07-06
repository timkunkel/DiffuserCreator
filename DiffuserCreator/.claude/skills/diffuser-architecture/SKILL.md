---
name: diffuser-architecture
description: How DiffuserCreator turns a grid definition into a sculpted wall of diffuser blocks — the DiffuserGrid → DepthShaper → DiffuserBlock pipeline, the three depth strategies (Cutting/Curve/Manual), the DiffuserSettings snapshot, the two curve modes (Height/Angle), the runtime control panel, and the selection/handle layer. Load this BEFORE changing how blocks are generated, positioned, depth-driven, or selected, or when asking "where does depth come from" / "how do I add a new depth strategy" / "what runs at Start".
---

# DiffuserCreator Architecture

A diffuser is a wall panel of many small cells at varying depths; the depth pattern scatters reflected sound instead of echoing it. This tool builds a **blueprint** for such a panel in Unity: a grid of cube blocks whose front faces are carved to different depths, arranged to be acoustically useful and nice to look at. Verified 2026-07-06 against `master` on **Unity 6000.3.9f1** (repo root `C:\_dev\DiffusorCreator\DiffuserCreator`). All gameplay scripts live in `Assets/Scripts/` in the `DiffuserCreator` namespace (UI in `DiffuserCreator.UI`, editor tools in `DiffuserCreator.EditorTools`).

## The layers

The code is split so that **what a block is** (geometry), **how its depth is decided** (behavior), **what the settings are** (data), and **who drives it** (orchestration) are separate concerns:

```
DiffuserGrid (orchestrator)          Assets/Scripts/DiffuserGrid.cs
  ├─ owns configuration + builds a DiffuserSettings snapshot
  ├─ spawns _rows × _columns of DiffuserBlock
  └─ picks a DepthShaper by DepthSource and applies it to every block
DepthShaper (behavior)               Assets/Scripts/DepthShaper.cs
  ├─ CuttingDepthShaper  → raycast against a cutting surface
  ├─ CurveDepthShaper    → evaluate AnimationCurves (Height or Angle)
  └─ ManualDepthShaper   → leave depths flat
DiffuserBlock (geometry)             Assets/Scripts/DiffuserBlock.cs
  └─ a dumb cube: size + 4 corner depths + mesh + collider + indicators
DiffuserSettings (data)              Assets/Scripts/DiffuserSettings.cs
  └─ immutable per-rebuild snapshot of the shaping parameters + the enums
```

This is the key change from the original prototype, where `DiffuserBlock` did everything (raycasting, curve evaluation, holding grid references). The block no longer knows what a curve or a cutting surface is.

## The generation pipeline

`DiffuserGrid.Start()` → `Generate()`. `Generate()` ([DiffuserGrid.cs](../../../Assets/Scripts/DiffuserGrid.cs)):

1. `DestroyBlocks()` — tears down any previously generated blocks.
2. Allocates `_blocks = new DiffuserBlock[_rows, _columns]`.
3. Computes total `Width`/`Height` from block size + spacing, centers the grid on the transform origin, and walks row-by-row placing each block. `CreateBlock(pos)` instantiates the prefab, calls `block.SetSize(w,h,d)`, and sets `block.NormalizedPosition` (the block's `[0,1]²` position in the grid, used by curve shaping).
4. Calls `Reshape()`.

`Reshape()` builds a `DiffuserSettings` via `BuildSettings()`, gets `DepthShaper.For(_depthSource)`, and calls `shaper.Shape(block, settings)` on every block. Structural changes (rows/columns/size/spacing) require `Generate()`; pure depth/curve changes only need `Reshape()` — the runtime UI wires each control to the right one.

Derived sizes:
- `Width  => _columns * _blockWidth  + (_columns - 1) * _horizontalSpacing`
- `Height => _rows    * _blockHeight + (_rows    - 1) * _verticalSpacing`

There is no per-frame `Update`; everything is driven by `Generate`/`Reshape`, `[ContextMenu]` actions, or the UI panel.

## DepthShaper — the three depth strategies

`DepthShaper` ([DepthShaper.cs](../../../Assets/Scripts/DepthShaper.cs)) is an abstract base with one method: `void Shape(DiffuserBlock block, DiffuserSettings settings)`. `DepthShaper.For(DepthSource)` is the factory. To **add a new way to drive depth**, subclass `DepthShaper` and add a case to the factory — you never touch `DiffuserBlock`.

- **`CuttingDepthShaper`** (`DepthSource.Cutting`) — raycasts from the block toward local −Z against `settings.CuttingLayerMask`; hit distance becomes depth, capped at `settings.DefaultDepth * block.Depth`, else `settings.DefaultDepth`. `settings.HeightMode` decides how many rays and how corners share a depth:
  - `Middle` — one ray at center, single depth.
  - `Corner` — four rays, four independent depths.
  - `Horizontal` — top edge and bottom edge each get one depth.
  - `Vertical` — left edge and right edge each get one depth.
  It reads the block's back-corner constants from `DiffuserBlock.BackCorners` and writes via `block.SetDepths(...)`.
- **`CurveDepthShaper`** (`DepthSource.Curve`) — drives depth from `AnimationCurve`s at `block.NormalizedPosition`, in one of two `CurveMode`s (below).
- **`ManualDepthShaper`** (`DepthSource.Manual`) — no-op; blocks stay flat at their initial depth (set in `SetSize`).

## Curves — CurveMode Height vs Angle

Both live in `CurveDepthShaper`:
- **`Height`**: evaluate the enabled horizontal/vertical curves at the normalized position, average if both on, and set a **single flat depth** `InitialDepth + value * InitialDepth`.
- **`Angle`**: evaluate enabled horizontal/vertical/**diagonal** curves (diagonal at `(x+y)/2`) to a target angle, snap it to `settings.SnapAngle` degrees via ProBuilder `Snapping.Snap`, store it in `block.Angle`, then compute a two-level depth (bottom edge at `InitialDepth`, top edge deeper) so the front face **tilts** by that angle. The tilt math (line-line intersection) is owned by `diffuser-mesh-geometry`.

Three independent curve toggles exist: `UseHorizontalCurve`, `UseVerticalCurve`, `UseDiagonalCurve`. (The serialized names on the grid keep the old misspelling via `[FormerlySerializedAs("UseDioganalCurve")]`/`("DioganalCurve")` — see `diffuser-build-and-run`.)

## DiffuserSettings — the shaping snapshot

`DiffuserSettings` ([DiffuserSettings.cs](../../../Assets/Scripts/DiffuserSettings.cs)) is a plain `[Serializable]` class holding `HeightMode`, `CurveMode`, the curve toggles + curves, `SnapAngle`, `CuttingLayerMask`, and `DefaultDepth`. The grid builds a fresh one each `Reshape()` (`BuildSettings()`), so shapers never reference the grid or its serialized fields — they get exactly the data they need. The enums `HeightMode`, `DepthSource`, and `CurveMode` also live in this file (top-level in the `DiffuserCreator` namespace).

> **Note on where settings are stored.** The grid still holds the settings as individual serialized fields (`_rows`, `_blockWidth`, `SelectedCurveMode`, the curves, …) rather than a nested settings object, because those values live in `MainScene.unity`/`DiffuserGrid.prefab` keyed by field name and nesting them would silently drop the scene overrides and authored curves. `DiffuserSettings` is a per-rebuild *snapshot* assembled from those fields, not the serialized storage. The grid exposes clean get/set **properties** (`Rows`, `BlockWidth`, `DepthSource`, `CurveMode`, …) that the UI binds to.

## DiffuserControlPanel — runtime UI

`DiffuserControlPanel` ([DiffuserControlPanel.cs](../../../Assets/Scripts/UI/DiffuserControlPanel.cs)) is a UI Toolkit panel (`UIDocument`) that builds sliders/toggles/enum-dropdowns/buttons in code and binds them to the grid's properties, calling `Generate()` for structural changes and `Reshape()` for shaping changes. Setup is owned by `diffuser-editor-workflows`. Curve *shapes* are still edited on the grid component in the inspector (no runtime curve editor).

## Selection layer (runtime, not generation)

Independent of generation:
- `SelectionManager` ([SelectionManager.cs](../../../Assets/Scripts/SelectionManager.cs)) — mouse raycast to hover/select a `SelectableBlock`, driving a `RuntimeTransformHandle` gizmo (third-party plugin in `Assets/Plugins/RuntimeTransformHandle`, **do not refactor**; the handle gets input first via `TryInteract`).
- `SelectableBlock` — swaps hover/selected materials, shows/hides the block's `VertexIndicator`s.
- `VertexIndicator` — a TextMeshPro label of a corner index, spawned per point by `DiffuserBlock.ShowIndicators()`.
- `CameraLookAt` — keeps a camera aimed at a target.
- `CuttingSurface` — an empty marker `MonoBehaviour`; only its collider matters (the shape `CuttingDepthShaper` carves against).

## What breaks if you get it wrong

- Renaming a serialized grid field without `[FormerlySerializedAs]` → the value in `DiffuserGrid.prefab`/`MainScene.unity` silently resets. You can't re-enter it without the editor. (`DiffuserBlock` no longer has serialized config beyond `_vertexIndicatorPrefab`.)
- Moving the grid's flat fields into a nested settings object → same data loss (property paths change), which is why they stay flat.
- A shaper that reaches back to the grid instead of using its `DiffuserSettings` argument breaks the decoupling and reintroduces the god-object problem.

## When NOT to use this skill

- Vertex indices, triangle winding, normals, the angle intersection math → `diffuser-mesh-geometry`.
- How to click the buttons / set up a cutting surface / the UI panel / export → `diffuser-editor-workflows`.
- Unity version, packages, serialization renames, offline compile check → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8` on Unity 6000.3.9f1 by reading the cited files and compiling all scripts clean. Re-verify the drift-prone claims:

```powershell
# Pipeline + shaper delegation
Select-String -Path "Assets\Scripts\DiffuserGrid.cs" -Pattern "Generate|Reshape|BuildSettings|DepthShaper.For|CreateBlock"
# Strategy factory + the three shapers
Select-String -Path "Assets\Scripts\DepthShaper.cs" -Pattern "class .*DepthShaper|DepthShaper For|HeightMode|CurveMode"
# Block is dumb geometry only
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "SetDepths|SetUniformDepth|NormalizedPosition|BackCorners"
# Settings snapshot + enums
Select-String -Path "Assets\Scripts\DiffuserSettings.cs" -Pattern "enum |class DiffuserSettings"
```

If any output contradicts this file, trust the code and update this skill in the same change.

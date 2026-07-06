---
name: diffuser-architecture
description: How DiffuserCreator turns a grid definition into a sculpted wall of diffuser blocks — the DiffuserGrid → DiffuserBlock → mesh pipeline, the three depth-editing modes (Cutting/Curve/Custom), the two curve modes (Height/Angle), block sequences, and the selection/handle layer. Load this BEFORE changing how blocks are generated, positioned, depth-driven, or selected, or when asking "where does depth come from" / "how do rows and columns share a curve" / "what runs at Start".
---

# DiffuserCreator Architecture

A diffuser is a wall panel of many small cells at varying depths; the depth pattern scatters reflected sound instead of echoing it. This tool builds a **blueprint** for such a panel in Unity: a grid of cube blocks whose front faces are carved to different depths, arranged to be acoustically useful and nice to look at. Everything below was verified against the code on 2026-07-06 (`master`, repo root `C:\_dev\DiffusorCreator\DiffuserCreator`). All gameplay scripts live in `Assets/Scripts/` and (currently) in the global namespace.

## The generation pipeline

`DiffuserGrid.Start()` → `Generate()`. `Generate()` ([DiffuserGrid.cs](../../../Assets/Scripts/DiffuserGrid.cs)):

1. Destroys any previously generated blocks.
2. Allocates `_blocks = new DiffuserBlock[_rows, _columns]`.
3. Computes total `Width`/`Height` from block size + spacing, centers the grid on the transform origin, and walks row-by-row, column-by-column placing each block at a `Vector2` position.
4. For each cell calls `CreateBlock(pos)`: `Instantiate(_blockPrefab, …, transform)` → `block.SetSize(w,h,d)` → `block.Initialize(this, pos, horizontalCurve, verticalCurve, diagonalCurve, curveMode)`.
5. Builds `_blockRows` and `_blockColumns` as `DiffuserBlockSequence[]` — one sequence per row (Horizontal) and per column (Vertical), each holding that line's blocks + the relevant curve.

Key derived sizes:
- `Width  => _columns * _blockWidth  + (_columns - 1) * _horizontalSpacing`
- `Height => _rows    * _blockHeight + (_rows    - 1) * _verticalSpacing`

`Update()` is currently empty (its `CutCubes()`/`UpdateCurveCubes()` calls are commented out). Regeneration and all mutations are driven by `[ContextMenu]` items, not per-frame.

## DiffuserBlock — one cell, owns its mesh

`DiffuserBlock` ([DiffuserBlock.cs](../../../Assets/Scripts/DiffuserBlock.cs)) is a `MonoBehaviour` requiring a `MeshFilter` + `MeshCollider`. It holds 8 corner points and a `float[4] _depth` (one depth per front-face corner). Mesh construction is owned by `diffuser-mesh-geometry` — this skill only covers *where depths come from*.

- Block **size** is set via `transform.localScale` (`SetSize`), so the local-space cube is always the unit cube `[-0.5, 0.5]³`; world size = scale.
- Depth is measured along local **−Z** (front face pushed toward −Z). `DEFAULT_DEPTH = 1f`.
- `Awake()` grabs components, sets default depth, `InitPoints()`, `BuildMesh()`.
- `Initialize(...)` records the block's absolute and **normalized** position in the grid (`_relativePosInGrid` in `[0,1]²`), copies the grid's curve toggles/curves, then applies depth and rebuilds.

### HeightMode — which corners move together

`HeightMode` selects the granularity of a cut/depth operation:
- `Middle` — single depth for all four corners (flat front, one raycast at center).
- `Corner` — four independent corner depths (four raycasts).
- `Horizontal` — top edge and bottom edge each get one depth (`SetDepthsBetweenIndices(0,1)` and `(2,3)`).
- `Vertical` — left edge and right edge each get one depth (`(0,3)` and `(1,2)`).

### HeightEditing — the three depth *sources*

`EditingMode` (`HeightEditing`) gates which method actually runs:
- **`Cutting`** — `CutWithSurface()` raycasts from the block along local −Z against `_cuttingLayerMask`; hit distance becomes depth, else `DEFAULT_DEPTH`. `HeightMode` decides how many rays and how corners map. Only runs when `EditingMode == Cutting`.
- **`Curve`** — `UpdateDepthWithCurve(curveMode)` drives depth from `AnimationCurve`s (below). Only runs when `EditingMode == Curve`.
- **`Custom`** — neither runs; depths are whatever was set manually.

This gating is a guard-clause `if (EditingMode != X) return;` at the top of each method — calling the wrong one for the current mode is a silent no-op by design.

## Curves — CurveMode Height vs Angle

`DiffuserGrid.CurveMode` has two values, both consumed in `DiffuserBlock.UpdateDepthWithCurve`:

- **`Height`** (`SetDepthWithHeightCurve`): evaluate the enabled horizontal/vertical curves at the block's normalized position, average them if both are on, and set a **single flat depth** `_initialDepth + value * _initialDepth`.
- **`Angle`** (`SetDepthWithAngleCurve`): evaluate enabled horizontal/vertical/**diagonal** curves (diagonal uses `(relX + relY) / 2`) to get a target angle, snap it to `_diffuserGrid.SnapAngle` degrees via ProBuilder `Snapping.Snap`, then compute a two-level depth (bottom edge at `_initialDepth`, top edge deeper) so the front face **tilts** by that angle. The tilt math (line-line intersection) is owned by `diffuser-mesh-geometry`.

Three independent curve toggles exist on the grid: `UseHorizontalCurve`, `UseVerticalCurve`, `UseDioganalCurve` (note the misspelling **"Dioganal"** — it is baked into serialized field names `DioganalCurve`/`UseDioganalCurve` and the private `_dioganalCurve`; renaming requires `[FormerlySerializedAs]`, see `diffuser-build-and-run`).

## DiffuserBlockSequence — a shared-curve row/column

`DiffuserBlockSequence` ([DiffuserBlockSequence.cs](../../../Assets/Scripts/DiffuserBlockSequence.cs)) is a plain C# class (not a `MonoBehaviour`) wrapping one row or column of blocks plus its `AnimationCurve`, `SequenceOrientation`, and `CurveMode`. Its only behavior is `SetCurve(curve)`: store it and push it to every block via `block.SetCurve(curve, _orientation, _curveMode)`. It's the intended handle for "retune this whole row's curve at once," though nothing calls `SetCurve` yet in the current scene wiring.

> **Known bug** in `DiffuserBlock.SetCurve`: the `Vertical` case sets `_useVerticalCurve = false` (should be `true`). A vertical sequence's curve is stored but immediately disabled. See `diffuser-mesh-geometry` / the refactor notes.

## Selection layer (runtime, not generation)

Independent of generation, a runtime selection stack lets you pick blocks and move them with a gizmo:
- `SelectionManager` ([SelectionManager.cs](../../../Assets/Scripts/SelectionManager.cs)) — raycasts from the mouse each frame (when not `Idle`), tracks hovered/selected `SelectableBlock`, and drives a `RuntimeTransformHandle` (third-party plugin, `Assets/Plugins/RuntimeTransformHandle`, **do not refactor**). The handle gets first crack at input (`TryInteract`) before selection is evaluated.
- `SelectableBlock` ([SelectableBlock.cs](../../../Assets/Scripts/SelectableBlock.cs)) — swaps hover/selected materials and shows/hides the block's `VertexIndicator`s.
- `VertexIndicator` ([VertexIndicator.cs](../../../Assets/Scripts/VertexIndicator.cs)) — a TextMeshPro label of a corner's index, spawned per point by `DiffuserBlock.ShowIndicators()`.
- `CameraLookAt` ([CameraLookAt.cs](../../../Assets/Scripts/CameraLookAt.cs)) — keeps a camera aimed at a target each frame.
- `CuttingSurface` ([CuttingSurface.cs](../../../Assets/Scripts/CuttingSurface.cs)) — currently an **empty** `MonoBehaviour`; it's just a tag/marker on a collider that blocks on the cutting layer raycast against. Its geometry is the "surface" that carves the diffuser.

## What breaks if you get it wrong

- Calling `CutWithSurface`/`UpdateDepthWithCurve` in the wrong `EditingMode` → silent no-op (the guard clause).
- Renaming a serialized grid/block field without `[FormerlySerializedAs]` → the value set in `DiffuserGrid.prefab`/`MainScene.unity` silently resets to default. You cannot re-enter it without opening the editor.
- Regeneration (`Generate()`) uses `Destroy`, which is deferred; calling it and immediately reading `_blocks` from the same frame is fine (the array is rebuilt synchronously) but the old GameObjects live one more frame.

## When NOT to use this skill

- Vertex indices, triangle winding, normals, the angle intersection math → `diffuser-mesh-geometry`.
- How to click the buttons / set up a cutting surface / export → `diffuser-editor-workflows`.
- Unity version, packages, editor-only-code build breakage, serialization renames → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8` by reading the cited files. Re-verify the drift-prone claims:

```powershell
# Pipeline entry + sequence construction
Select-String -Path "Assets\Scripts\DiffuserGrid.cs" -Pattern "Generate|CreateBlock|DiffuserBlockSequence|CurveMode"
# Depth sources + mode gating
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "HeightEditing|HeightMode|EditingMode !=|UpdateDepthWithCurve|CutWithSurface"
# The SetCurve vertical bug
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "_useVerticalCurve"
# Misspelled diagonal field names
Select-String -Path "Assets\Scripts\*.cs" -Pattern "Dioganal"
```

If any output contradicts this file, trust the code and update this skill in the same change.

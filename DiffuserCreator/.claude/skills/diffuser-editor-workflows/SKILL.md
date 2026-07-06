---
name: diffuser-editor-workflows
description: How to actually operate DiffuserCreator in the Unity editor — the DiffuserGrid ContextMenu actions (Generate/Cut/Rotate/Offset/Print/Save), how to set up a CuttingSurface, how to configure curves and snap angle, the runtime block selection/gizmo, and the three export paths (OBJ, mesh .asset, FBX). Load this when someone asks "how do I generate/regenerate the wall" / "how do I carve it with a surface" / "how do I export the result".
---

# DiffuserCreator Editor Workflows

This is the operator's manual: the buttons, the setup, the outputs. Verified 2026-07-06 against `master`. The scene is `Assets/Scenes/MainScene.unity`; the tool lives on `DiffuserGrid.prefab` with child `DiffuserBlock`/`CuttingSurface`/`VertexIndicator` prefabs in `Assets/Prefabs/`.

## Generating the wall

`DiffuserGrid` inspector fields drive everything: `_rows`, `_columns`, `_horizontalSpacing`, `_verticalSpacing`, `_blockWidth/Height/Depth`, `_blockPrefab`, the curve toggles/curves, `SelectedCurveMode`, and `SnapAngle`.

`DiffuserGrid` `[ContextMenu]` actions (right-click the component header in the Inspector):
- **Generate Grid** (`Generate`) — (re)builds the whole grid. Also runs automatically in `Start()` on Play. Destroys existing blocks first, so it's safe to press repeatedly.
- **Cut with Surface** (`CutCubes`) — calls `CutWithSurface()` on every block. Only blocks whose `EditingMode == Cutting` respond.
- **Rotate 90°** (`Rotate90`) — rotates every block 90° about `Vector3.back`.
- **Offset X** (`OffsetX`) — shifts every *other* row (step of 2) by +0.5 in local X, for a brick-style stagger.
- **Print Grid** (`PrintGrid`) — histograms blocks by their snapped `Angle` and logs the count per angle (used to sanity-check how many distinct facet angles a design needs). Currently logs via `Debug.LogError` — noisy but harmless.
- **Save as Mesh** (`SaveAsMesh`) — see Export below.

## Carving with a cutting surface

The `Cutting` editing mode sculpts blocks against real geometry:
1. Put a mesh (the desired relief shape) on a GameObject, give it a collider, and place it on the **cutting layer** (the layer selected in each block's `_cuttingLayerMask` and the grid's `CuttingSurface`).
2. Set the blocks' `EditingMode = Cutting` and choose a `HeightMode` (`Middle` = flat per block, `Corner` = fully sculpted, `Horizontal`/`Vertical` = edge-wise).
3. Run **Cut with Surface**. Each block raycasts along local −Z; the hit distance becomes that corner/edge/center depth, capped at `DEFAULT_DEPTH * Depth`; a miss falls back to `DEFAULT_DEPTH`.

`CuttingSurface.cs` is an empty marker component — the actual surface is just its collider mesh. See `diffuser-architecture` for mode/gating detail.

## Configuring curves

For the `Curve` editing mode (no external geometry needed):
1. Set blocks' `EditingMode = Curve`.
2. On `DiffuserGrid`, enable any of `UseHorizontalCurve` / `UseVerticalCurve` / `UseDioganalCurve` (sic) and author the matching `AnimationCurve` (`HorizontalCurve`/`VerticalCurve`/`DioganalCurve`). Curves are evaluated at the block's normalized `[0,1]` grid position.
3. Choose `SelectedCurveMode`:
   - `Height` — curve value scales each block's flat depth.
   - `Angle` — curve value becomes a face tilt angle, snapped to `SnapAngle` degrees (see `diffuser-mesh-geometry` for the tilt math).
4. Regenerate. `Initialize()` applies the curve during generation; there is no live "update curves" button wired in (`UpdateCurveCubes` exists but is commented out in `Update`).

## Selecting and moving blocks at runtime

In Play mode, `SelectionManager` (on a scene object) lets you hover (material swap + vertex indicators) and click-select a `SelectableBlock`, which activates a `RuntimeTransformHandle` position gizmo on it. Set `_currentMode` off `Idle` to enable. The gizmo consumes input first, so dragging it won't deselect. `CameraLookAt` keeps the camera aimed at its target for orbiting.

## Exporting the result

Three paths, all **editor-only** (they use `UnityEditor` APIs — see `diffuser-build-and-run` for the build implication):

1. **Combined mesh `.asset`** — `DiffuserGrid` → **Save as Mesh**. Combines all child `MeshFilter`s (`Mesh.CombineMeshes`) into one mesh and writes it via a Save-File panel into `Assets/GeneratedMesh` (`SaveMesh(...)`, makes a new instance, optional `MeshUtility.Optimize`). Existing outputs: `Assets/Meshes/*.asset`.
2. **Wavefront OBJ** — menu **File → Export → Wavefront OBJ** (with/without submeshes), from `ObjExporter`/`ObjExporterScript` ([ObjExporterScript.cs](../../../Assets/Scripts/ObjExporterScript.cs)). Exports the current `Selection` (first selected GameObject and its children), temporarily zeroing its position, flipping Z to match OBJ's coordinate handedness. Prior output: `Assets/GeneratedMesh/*.obj`.
3. **FBX** — via the `com.unity.formats.fbx` package (v5.1.4). Use Unity's built-in **GameObject → Export To FBX…** on the generated grid. Prior output: `Assets/GeneratedMesh/geil.fbx`.

For a physical build, OBJ/FBX feed CAD/CAM or a 3D printer / CNC that cuts the real diffuser panel.

## When NOT to use this skill

- Why a mode does what it does, curve averaging rules → `diffuser-architecture`.
- Mesh internals, winding, angle math → `diffuser-mesh-geometry`.
- Editor-only-code breaks player builds, package versions, opening the project → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8`. Re-verify:

```powershell
# ContextMenu actions
Select-String -Path "Assets\Scripts\DiffuserGrid.cs" -Pattern "ContextMenu|SaveAsMesh|SaveMesh"
# OBJ export menu items
Select-String -Path "Assets\Scripts\ObjExporterScript.cs" -Pattern "MenuItem|DoExport"
# FBX package present
Select-String -Path "Packages\manifest.json" -Pattern "formats.fbx"
```

If output contradicts this file, trust the code and update this skill in the same change.

---
name: diffuser-editor-workflows
description: How to actually operate DiffuserCreator — the DiffuserGrid ContextMenu actions (Generate/Reshape/Rotate/Offset/Print/Save), how to set up a CuttingSurface, how to configure curves and snap angle, the runtime UI Toolkit control panel (and its one-click editor setup), and the three export paths (OBJ, mesh .asset, FBX). Load this when someone asks "how do I generate/regenerate the wall" / "how do I carve it with a surface" / "how do I tweak it at runtime" / "how do I export the result".
---

# DiffuserCreator Editor Workflows

The operator's manual: the buttons, the setup, the outputs. Verified 2026-07-06 against `master` on Unity 6000.3.9f1. The scene is `Assets/Scenes/MainScene.unity`; the tool lives on `DiffuserGrid.prefab` with child `DiffuserBlock`/`CuttingSurface`/`VertexIndicator` prefabs in `Assets/Prefabs/`.

## Generating and reshaping the wall

`DiffuserGrid` inspector fields drive everything: `_rows`, `_columns`, spacings, block `_blockWidth/Height/Depth`, `_blockPrefab`, `_depthSource`, `_heightMode`, `_cuttingLayerMask`, `_defaultDepth`, the curve toggles/curves, `SelectedCurveMode`, `SnapAngle`.

Two rebuild verbs:
- **Generate** — full rebuild: destroys and re-instantiates all blocks, re-lays them out, then reshapes. Needed after changing grid size, block size, or spacing. Runs automatically in `Start()`.
- **Reshape** — keeps the existing blocks and only recomputes their depths with the current `DepthSource` + settings. Enough for depth-source, height-mode, curve-mode, curve-toggle, snap-angle, or default-depth changes.

`DiffuserGrid` `[ContextMenu]` actions (right-click the component header):
- **Generate Grid** (`Generate`)
- **Reshape Blocks** (`Reshape`) — applies whichever `DepthSource` is selected (this replaces the old separate "Cut with Surface" / "Update Curves" menus).
- **Offset X** (`OffsetX`) — shifts every other row by +0.5 X for a brick stagger.
- **Rotate 90°** (`Rotate90`)
- **Print Grid** (`PrintGrid`) — histograms blocks by snapped `Angle` (`Debug.Log`).
- **Save as Mesh** (`SaveAsMesh`) — editor-only; see Export.

## Carving with a cutting surface

To sculpt blocks against real geometry:
1. Put a mesh (the desired relief shape) on a GameObject with a collider, on the **cutting layer**.
2. On `DiffuserGrid`, set `DepthSource = Cutting`, choose the `HeightMode` (`Middle`=flat per block, `Corner`=fully sculpted, `Horizontal`/`Vertical`=edge-wise), and set `_cuttingLayerMask` to that layer.
3. Run **Reshape Blocks** (or Play/Generate). Each block raycasts along local −Z; hit distance becomes that corner/edge/center depth (capped at `DefaultDepth * Depth`), a miss falls back to `DefaultDepth`.

`CuttingSurface.cs` is an empty marker — the actual surface is its collider mesh. The cutting layer mask now lives on the **grid** (`_cuttingLayerMask`), not on each block.

## Configuring curves

For `DepthSource = Curve` (no external geometry):
1. Set `DepthSource = Curve`.
2. Enable any of `UseHorizontalCurve` / `UseVerticalCurve` / `UseDiagonalCurve` and author the matching `AnimationCurve`. Curves are evaluated at the block's normalized `[0,1]` grid position.
3. Choose `SelectedCurveMode`: `Height` (curve scales flat depth) or `Angle` (curve becomes a face tilt, snapped to `SnapAngle` degrees — see `diffuser-mesh-geometry`).
4. Reshape.

## Runtime control panel (UI Toolkit)

`DiffuserControlPanel` ([DiffuserControlPanel.cs](../../../Assets/Scripts/UI/DiffuserControlPanel.cs)) is a floating panel that exposes every setting as sliders/toggles/enum-dropdowns plus Regenerate/Reshape/Print buttons, so you can tune the wall in Play mode without the inspector. Structural controls call `Generate()`; shaping controls call `Reshape()`. Curve *shapes* are still authored on the grid component (no runtime curve editor).

**One-click setup:** menu **Tools → DiffuserCreator → Create Control Panel** ([DiffuserControlPanelSetup.cs](../../../Assets/Scripts/Editor/DiffuserControlPanelSetup.cs)) creates a GameObject with a `UIDocument` + `DiffuserControlPanel`, wires it to the scene's `DiffuserGrid`, and creates a `PanelSettings` asset if the project has none.

**Manual setup:** add a GameObject → `UIDocument` (assign a `PanelSettings`; create one via *Assets ▸ Create ▸ UI Toolkit ▸ Panel Settings Asset*, which also generates a default runtime theme) → add `DiffuserControlPanel` and assign the `DiffuserGrid`. Requires a `PanelSettings` with a theme to render — that's standard UI Toolkit runtime setup.

## Exporting the result

Three paths, all **editor-only** (see `diffuser-build-and-run`):
1. **Combined mesh `.asset`** — `DiffuserGrid` → **Save as Mesh**. Combines all child `MeshFilter`s into one mesh via a Save-File panel into `Assets/GeneratedMesh`. Existing outputs: `Assets/Meshes/*.asset`.
2. **Wavefront OBJ** — menu **File → Export → Wavefront OBJ** (with/without submeshes), from `ObjExporter`/`ObjExporterScript` ([ObjExporterScript.cs](../../../Assets/Scripts/ObjExporterScript.cs)). Exports the current `Selection`, zeroing its position and flipping Z for OBJ handedness.
3. **FBX** — via `com.unity.formats.fbx`; use Unity's **GameObject → Export To FBX…** on the generated grid.

OBJ/FBX feed CAD/CAM or a 3D printer / CNC that cuts the real diffuser panel.

## Runtime selection

In Play mode, `SelectionManager` (set `_currentMode` off `Idle`) lets you hover (material + vertex indicators) and click-select a block, activating a `RuntimeTransformHandle` gizmo. `CameraLookAt` keeps the camera aimed for orbiting.

## When NOT to use this skill

- Why a mode does what it does, curve averaging, the shaper contract → `diffuser-architecture`.
- Mesh internals, winding, angle math → `diffuser-mesh-geometry`.
- Editor-only-code build implications, package versions, serialization → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8`. Re-verify:

```powershell
Select-String -Path "Assets\Scripts\DiffuserGrid.cs" -Pattern "ContextMenu|Generate|Reshape|SaveAsMesh"
Select-String -Path "Assets\Scripts\Editor\DiffuserControlPanelSetup.cs" -Pattern "MenuItem|PanelSettings|UIDocument"
Select-String -Path "Assets\Scripts\ObjExporterScript.cs" -Pattern "MenuItem|DoExport"
Select-String -Path "Packages\manifest.json" -Pattern "formats.fbx"
```

If output contradicts this file, trust the code and update this skill in the same change.

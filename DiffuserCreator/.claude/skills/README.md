# DiffuserCreator Skill Library

Working knowledge of this project for engineers and AI sessions with no prior context. Each `<name>/SKILL.md` is self-contained, states when NOT to use it, and ends with re-verification commands — if code and skill disagree, the code wins; update the skill in the same change.

## Where to start

| Your situation | Load first |
|---|---|
| Understanding what the tool does, how a grid becomes blocks, the shaper strategy, curves | `diffuser-architecture` |
| Touching mesh construction, vertex/triangle layout, depth math, the angle-tilt intersection | `diffuser-mesh-geometry` |
| Actually using the tool: context menus, cutting surface, curve setup, the runtime UI panel, export | `diffuser-editor-workflows` |
| Opening/compiling the project, Unity/package versions, serialization traps, offline compile check | `diffuser-build-and-run` |

## The one-paragraph model

`DiffuserGrid` (a `MonoBehaviour`) spawns an `_rows × _columns` grid of `DiffuserBlock` prefab instances and delegates their depth to a **`DepthShaper`** strategy. A `DiffuserBlock` is now a *dumb* geometry object: a cube whose four front-face corners are pushed to independent depths, owning only its mesh, collider, and vertex indicators. The chosen `DepthShaper` (`CuttingDepthShaper`, `CurveDepthShaper`, or `ManualDepthShaper`, picked by the grid's `DepthSource`) computes those depths from the grid's single **`DiffuserSettings`** config object — `Cutting` raycasts against a `CuttingSurface` collider, `Curve` evaluates `AnimationCurve`s by the block's normalized grid position (in `Height` or `Angle` `CurveMode`), `Manual` leaves them flat. The visible wall is a sculpted relief that both scatters sound and looks intentional. A runtime **`DiffuserControlPanel`** (UI Toolkit, laid out in `DiffuserControlPanel.uxml`) binds every setting to `grid.Settings` for live tweaking; meshes can be exported as OBJ / mesh `.asset` / FBX.

## Layer map

| Layer | Files |
|---|---|
| Data | `DiffuserSettings.cs` (enums `HeightMode`/`DepthSource`/`CurveMode` + full config class) |
| Geometry | `DiffuserBlock.cs`, `GeometryUtils.cs` |
| Behavior | `DepthShaper.cs` (`CuttingDepthShaper`, `CurveDepthShaper`, `ManualDepthShaper`) |
| Orchestration | `DiffuserGrid.cs` (owns one serialized `DiffuserSettings`) |
| Presentation | `UI/DiffuserControlPanel.cs` + `Assets/UI/DiffuserControlPanel.{uxml,uss}` + `DiffuserRuntimeTheme.tss`; `Editor/DiffuserControlPanelSetup.cs` |
| Runtime selection | `SelectionManager.cs`, `SelectableBlock.cs`, `VertexIndicator.cs`, `CameraLookAt.cs` |
| Export | `ObjExporterScript.cs`, `DiffuserGrid.SaveAsMesh` |

Authored 2026-07-06 from the repo at `master` HEAD `93b81d8`, on **Unity 6000.3.9f1** (the project was upgraded from 2022.1.15f1 during resurrection). Maintenance rule: when you change block/grid geometry, the shaper contract, or field names, re-run the skill's "Provenance and maintenance" commands and update it in the same change.

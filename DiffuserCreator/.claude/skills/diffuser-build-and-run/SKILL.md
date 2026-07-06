---
name: diffuser-build-and-run
description: Opening, compiling, and safely changing DiffuserCreator — the exact Unity/package versions, the two traps that bite hardest (editor-only APIs in runtime scripts break a player build; renaming serialized fields silently loses prefab/scene values), the ProBuilder/TextMeshPro/UI-Toolkit/FBX dependencies, the vendored-but-unwired PDFsharp, an offline Roslyn compile check, and how to verify without CI. Load this BEFORE renaming a serialized field, moving export code, adding a namespace/asmdef, or when the project won't compile/open.
---

# DiffuserCreator Build & Environment

How to work on this project without breaking it. No CI, no test project — verification is manual in the editor, but you can compile-check offline (below). Verified 2026-07-06 against `master`.

## Versions & dependencies

- **Unity 6000.3.9f1** (Unity 6.3). `ProjectSettings/ProjectVersion.txt`. The project was **upgraded from 2022.1.15f1** during resurrection (the `update project` commit / a later re-open) — older skill notes and any doc still saying 2022.1 are stale. Installed editors on this machine: `6000.2.6f2`, `6000.3.9f1`.
- Key packages (`Packages/manifest.json`): `com.unity.probuilder`, `com.unity.formats.fbx`, `com.unity.timeline`, `com.unity.ugui` + `com.unity.ui.builder` (UI Toolkit), TextMesh Pro.
- **ProBuilder is a code dependency**: `CurveDepthShaper.ShapeWithAngle` calls `UnityEngine.ProBuilder.Snapping.Snap`. Removing ProBuilder breaks compilation.
- **TextMesh Pro** is a code dependency via `VertexIndicator` (`TMPro.TextMeshPro`).
- **UI Toolkit** (`UnityEngine.UIElements`) is a code dependency via `DiffuserControlPanel`; the runtime panel needs a `PanelSettings` (with a real theme) + the UXML in the scene's `UIDocument`, plus an `EventSystem` for input (all handled by the setup menu — see `diffuser-editor-workflows`).
- **uGUI** (`UnityEngine.UI`) is referenced only for the `EventSystem`/`StandaloneInputModule` the setup menu adds (legacy Input Manager; no Input System package installed).

## Source layout (all in `DiffuserCreator` namespace)

| File | Role |
|---|---|
| `DiffuserSettings.cs` | Enums (`HeightMode`/`DepthSource`/`CurveMode`) + the full config class (single source of truth) |
| `GeometryUtils.cs` | `LineLineIntersection` |
| `DepthShaper.cs` | Strategy base + `Cutting`/`Curve`/`Manual` shapers |
| `DiffuserBlock.cs` | Dumb cube geometry + mesh + indicators |
| `DiffuserGrid.cs` | Orchestrator; owns one `[SerializeField] private DiffuserSettings _settings` (+`Settings` accessor + migration); `[ContextMenu]`s; editor mesh export |
| `UI/DiffuserControlPanel.cs` | Runtime panel; binds `Assets/UI/DiffuserControlPanel.uxml` controls to `grid.Settings` (`DiffuserCreator.UI`) |
| `Editor/DiffuserControlPanelSetup.cs` | Idempotent setup menu: theme + UXML + EventSystem + grid (`DiffuserCreator.EditorTools`) |
| `SelectionManager` / `SelectableBlock` / `VertexIndicator` / `CameraLookAt` / `CuttingSurface` | Runtime selection + markers |
| `ObjExporterScript.cs` | OBJ export (editor-only) |

UI assets live in `Assets/UI/`: `DiffuserControlPanel.uxml` (layout), `DiffuserControlPanel.uss` (style), `DiffuserRuntimeTheme.tss` (the runtime theme, imports Unity defaults), `DiffuserPanelSettings.asset`.

`Assets/Scripts/` has **no asmdef**; everything compiles into `Assembly-CSharp` (runtime), except files under `Assets/Scripts/Editor/` (the special `Editor` folder name makes them editor-only) and code behind `#if UNITY_EDITOR`.

## Trap 1 — editor-only APIs in runtime scripts

`UnityEditor` is stripped from player builds, so any runtime-assembly code using it won't compile in a build. In this project the editor-only code is correctly isolated:
- `ObjExporterScript.cs` — entire file wrapped in `#if UNITY_EDITOR`.
- `DiffuserGrid.SaveAsMesh`/`SaveMesh` — wrapped in `#if UNITY_EDITOR` (uses `EditorUtility`, `FileUtil`, `AssetDatabase`, `MeshUtility`).
- `Editor/DiffuserControlPanelSetup.cs` — in an `Editor/` folder **and** `#if UNITY_EDITOR`.

Keep it that way: any new `UnityEditor` use goes behind `#if UNITY_EDITOR` or into `Assets/Scripts/Editor/`.

## Trap 2 — renaming serialized fields loses data

`DiffuserGrid` config is stored in `DiffuserGrid.prefab` and, importantly, as **prefab-instance overrides in `MainScene.unity`** (the prefab itself has zeros; the real rows/columns/sizes and authored curves are scene overrides). Unity serializes by **field name / property path**. Renaming a serialized field — or **moving it into a nested object** — changes the path and silently drops the stored value.

The grid's config was moved into a nested `DiffuserSettings _settings`, so the old flat fields are **retained as `[SerializeField, HideInInspector]` legacy** and copied into `_settings` once via `ISerializationCallbackReceiver.OnAfterDeserialize` (guarded by `_migratedToSettings`; a `hasLegacyData` check stops a freshly added component from overwriting defaults with zeros). This preserves the scene overrides + authored curves without any `FormerlySerializedAs`. After every DiffuserGrid asset has been opened and re-saved, the legacy fields are dead and removable.

For a simple rename (not a move), `[FormerlySerializedAs("oldName")]` is still the tool. Private non-serialized fields rename freely. `DiffuserBlock` has no serialized config beyond `_vertexIndicatorPrefab`.

Adding a namespace to a `MonoBehaviour` is safe (Unity keys components by the script file GUID in the `.cs.meta`, not the type name) — but never rename the class or `.cs` file.

## Trap 3 — a PanelSettings without a real theme = invisible UI

A runtime UI Toolkit `PanelSettings` renders nothing usable unless its `themeStyleSheet` imports Unity's defaults. The theme `.tss` **must** contain `@import url("unity-theme://default");` — a file with only `VisualElement {}` leaves every control unstyled/invisible (a real bug hit here). `Assets/UI/DiffuserRuntimeTheme.tss` is the correct one and the setup menu always assigns it. Symptom of a bad theme: the panel GameObject exists and is enabled but nothing (or unstyled junk) shows in Play mode.

## Offline compile check (no editor launch)

You can validate scripts without opening Unity, using the installed editor's Roslyn + managed DLLs. This caught real issues during the refactor. Sketch (git-bash):

```bash
ED="/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Data"
API="$ED/MonoBleedingEdge/lib/mono/4.7.1-api"
# response file: -target:library -nostdlib+ -noconfig -define:UNITY_EDITOR
#   -r each of: $API/{mscorlib,System,System.Core,System.Xml}.dll, $API/Facades/netstandard.dll,
#     ALL $ED/Managed/UnityEngine/*.dll  (the *Module.dll set — do NOT also add the monolithic
#     UnityEditor.dll or MenuItem etc. become CS0433 duplicates),
#     Library/ScriptAssemblies/{Unity.ProBuilder,Unity.TextMeshPro,RuntimeTransformHandle}.dll
#   then every Assets/Scripts/**/*.cs
# IMPORTANT: quote every path (spaces in "Program Files") and use Windows paths (cygpath -w).
dotnet "$ED/DotNetSdkRoslyn/csc.dll" "@build.rsp"
```
A clean run (0 `error CS`) means the scripts compile against the real Unity assemblies. It won't catch Unity serialization/asset issues — only the editor does that.

## PDFsharp — vendored but not wired in

`PDFsharp/` at the repo root is a separate solution (`PdfSharp.sln`), not under `Assets/`, so Unity doesn't compile it and no script references it. Treat it as dead weight until a PDF-blueprint feature is actually built.

## Generated files & third-party code

- Root `Assembly-CSharp.csproj`, `*.sln`, `.DotSettings` are regenerated by Unity/Rider — don't hand-edit or commit; they churn after any editor open. Don't commit `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`.
- `Assets/Plugins/RuntimeTransformHandle/` (namespace `RuntimeHandle`) is vendored — leave it alone; only `SelectionManager` consumes it.

## Verifying a change (no CI)

1. Open in Unity 6000.3.9f1; let it recompile. Watch the Console (fastest signal). Or run the offline compile check above.
2. Open `MainScene.unity`, press Play — `DiffuserGrid.Start()` regenerates the wall.
3. Exercise `[ContextMenu]` actions (Generate / Reshape / Rotate / Offset / Print / Save) and the runtime control panel.
4. After a serialized-field change, confirm the prefab/scene still shows configured values (not reset) — catches a missing `[FormerlySerializedAs]`.

## When NOT to use this skill

- What the tool does / how depth is decided → `diffuser-architecture`.
- Mesh/vertex/angle math → `diffuser-mesh-geometry`.
- Which button to press, the UI panel, export → `diffuser-editor-workflows`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8`. Re-verify:

```powershell
Get-Content "ProjectSettings\ProjectVersion.txt"
Select-String -Path "Assets\Scripts\*.cs","Assets\Scripts\UI\*.cs","Assets\Scripts\Editor\*.cs" -Pattern "using UnityEditor|UNITY_EDITOR"
Select-String -Path "Assets\Scripts\DepthShaper.cs" -Pattern "ProBuilder|Snapping"
Test-Path "PDFsharp\PdfSharp.sln"
Get-ChildItem "Assets\Scripts" -Recurse -Filter *.asmdef   # expect none today
```

If output contradicts this file, trust the code and update this skill in the same change.

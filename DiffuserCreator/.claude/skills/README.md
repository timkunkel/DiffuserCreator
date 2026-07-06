# DiffuserCreator Skill Library

Working knowledge of this project for engineers and AI sessions with no prior context. Each `<name>/SKILL.md` is self-contained, states when NOT to use it, and ends with re-verification commands — if code and skill disagree, the code wins; update the skill in the same change.

## Where to start

| Your situation | Load first |
|---|---|
| Understanding what the tool does, how a grid becomes blocks, depth modes, curves, sequences | `diffuser-architecture` |
| Touching mesh construction, vertex/triangle layout, depth math, the angle-curve intersection | `diffuser-mesh-geometry` |
| Actually using the tool: context menus, cutting surface, curve setup, selection handles, export | `diffuser-editor-workflows` |
| Opening the project, Unity/package versions, editor-only-code gotchas, PDFsharp, known traps | `diffuser-build-and-run` |

## The one-paragraph model

`DiffuserGrid` (a `MonoBehaviour` on `DiffuserGrid.prefab`) spawns an `_rows × _columns` grid of `DiffuserBlock` prefab instances. Each `DiffuserBlock` builds its **own** mesh at runtime: a cube whose four front-face corners are pushed out to independent depths. Those depths come from one of three `HeightEditing` modes — `Cutting` (raycast against a `CuttingSurface` collider), `Curve` (evaluate `AnimationCurve`s by the block's normalized grid position, in `Height` or `Angle` `CurveMode`), or `Custom` (manual). The visible surface of the whole wall is therefore a sculpted relief that both scatters sound and looks intentional. Meshes can be combined and exported as an OBJ, a Unity mesh `.asset`, or FBX.

Authored 2026-07-06 from the repo at `master` HEAD `93b81d8`. Maintenance rule: when you change block/grid geometry or field names, re-run the skill's "Provenance and maintenance" commands and update it in the same change.

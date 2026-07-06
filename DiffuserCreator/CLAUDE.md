# DiffuserCreator — Claude Code Instructions

> Architecture, components, and project structure are in [AGENTS.md](AGENTS.md). Task-specific deep dives live in the skill library — start at [.claude/skills/README.md](.claude/skills/README.md) for the routing table.

## Code Style

Adopted from the WeAre team's convention (shared across their Unity projects):

- **No comments** unless the WHY is non-obvious (a hidden constraint, a subtle invariant, a workaround). Never write a comment that just restates what the code does. Delete Unity's boilerplate `// Start is called before the first frame update` stubs.
- **Curly braces always**, even for single-line `if` bodies.
- Single-line `if` returns stay on one line: `if (condition) { return value; }`
- **Meaningful names over abbreviations.** No explanatory comments as a substitute for clear naming. (Fix misspellings like "Dioganal" → "Diagonal" — but see the serialization rule below.)
- Match the existing `.editorconfig`/ReSharper formatting: 4-space indent, `_camelCase` private fields, `PascalCase` public members, `ALL_UPPER` constants, aligned consecutive assignments/declarations (the codebase already does this).
- Use `#region` blocks to group logical sections in larger files (`#region MonoBehaviour methods`, `#region Mesh construction`, …).
- One first-party namespace tree: gameplay scripts in `DiffuserCreator`, UI in `DiffuserCreator.UI`, editor tools in `DiffuserCreator.EditorTools`. Do **not** rename the `MonoBehaviour` classes or their `.cs` files (breaks prefab/scene component references).
- Keep the layering: `DiffuserBlock` is dumb geometry, depth decisions live in `DepthShaper` strategies fed the grid's single serialized `DiffuserSettings`, `DiffuserGrid` orchestrates. All config is `DiffuserSettings` (exposed as `grid.Settings`), not a public field/property pile; the UI is the only external consumer. A new way to drive depth = a new `DepthShaper` subclass + a factory case, never new logic in the block. (See the `diffuser-architecture` skill.)

## Hard Rules (this project)

- **Never rename a `public` or `[SerializeField]` field without `[FormerlySerializedAs("oldName")]`.** Config is serialized by field name in `DiffuserGrid.prefab`, `DiffuserBlock.prefab`, `SelectableBlock`, and `MainScene.unity`; a bare rename silently wipes the stored value and you cannot re-enter it without opening the editor. This is the #1 way to break the project. (See the `diffuser-build-and-run` skill.)
- **Keep `Assets/Scripts/` free of `UnityEditor` at runtime scope.** Editor-only code (`ObjExporter`, `DiffuserGrid.SaveMesh`) must be `#if UNITY_EDITOR`-guarded or live under an `Editor/` asmdef, or a player build won't compile.
- **Do not touch `Assets/Plugins/RuntimeTransformHandle/`** — vendored third-party plugin.
- **Do not commit generated files:** root `*.csproj`/`*.sln`, `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`, `.DotSettings.user`.
- ProBuilder (`Snapping.Snap`) and TextMesh Pro are **code** dependencies — don't remove the packages.

## Verifying changes (no CI, no tests)

There is no automated test suite. To verify:
1. Open in Unity 6000.3.9f1, let it recompile, check the Console for errors (fastest signal). Or compile-check offline with the installed editor's Roslyn (recipe in the `diffuser-build-and-run` skill) — this caught real errors during the refactor.
2. Open `Assets/Scenes/MainScene.unity`, press Play — `DiffuserGrid` regenerates the wall.
3. Exercise the `DiffuserGrid` `[ContextMenu]` actions (Generate Grid / Reshape Blocks / Rotate 90° / Offset X / Print Grid / Save as Mesh), and the runtime control panel (Tools ▸ DiffuserCreator ▸ Create Control Panel).
4. After any serialized-field change, confirm the prefab/scene still shows configured values (not reset to defaults) — this catches a missing `[FormerlySerializedAs]`.

## Conventions

- Regeneration is driven by `[ContextMenu]` actions, not per-frame `Update`. Prefer adding a context-menu entry over polling in `Update`.
- Use `Debug.Log`/`Debug.LogWarning` for informational logging; reserve `Debug.LogError` for actual errors (the prototype misuses `LogError` — don't copy that).
- All block geometry is computed in **local** space (unit cube scaled by `transform.localScale`); keep it that way.

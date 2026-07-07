# Papercraft Export

Self-contained module that unfolds a 3D mesh into a printable, flat papercraft net with glue
tabs — the same idea as Pepakura Designer, scoped to the simple box-like blocks this project
produces. No dependencies beyond `UnityEngine` (its own asmdef, `DiffuserCreator.Papercraft`,
auto-referenced so `Assembly-CSharp` can call it); nothing here uses `UnityEditor`, so it is
runtime-safe.

## Pipeline

Each file is one stage, kept separate so it can be unit-tested in isolation:

| Stage | File | What it does |
|---|---|---|
| Model + dual graph | `PapercraftModel.cs` | Welds duplicated verts, merges coplanar triangles into polygon faces, builds the face-adjacency (dual) graph with per-edge dihedral angles. |
| Spanning tree | `SpanningTreeBuilder.cs` | Minimum spanning forest weighted by dihedral angle → tree edges are folds, the rest are cuts. Flattest edges fold, sharp creases cut. |
| Unfold | `Unfolder.cs` | Roots each fold-connected component, walks the tree, rigidly places each child face across its shared edge (isometry — edge lengths exact). |
| Overlap check | `Polygon2D.cs`, `OverlapResolver.cs` | 2D polygon intersection between placed faces; on overlap, re-cuts the offending fold edge and re-unfolds. Warns. Box meshes never trigger it. |
| Glue tabs | `TabGenerator.cs` | Trapezoidal tab on exactly one side of every cut edge, tapering inward; flips/shrinks if it would collide. |
| Layout + render | `PieceDrawing.cs`, `PageLayout.cs`, `SvgRenderer.cs`, `PdfRenderer.cs` | Shelf-packs pieces onto A4/Letter pages, emits SVG and a self-contained PDF. Cut = solid, fold = dashed, matched labels per cut-edge pair, crop marks. |

## API

```csharp
using DiffuserCreator.Papercraft;

// From a single Unity Mesh:
PapercraftResult result = PapercraftExporter.Export(mesh, new PapercraftOptions
{
    MillimetersPerModelUnit = 100f,          // print scale (1 model unit -> 100 mm)
    PageSizeMm              = PapercraftOptions.PAGE_A4_MM,   // or PAGE_LETTER_MM
    TabHeightMm            = 8f,
    // ...see PapercraftOptions for the rest
});

result.SvgPages;  // string[] — one standalone SVG per page (mm, true scale)
result.PdfBytes;  // byte[]   — one multi-page PDF
```

`Export` also accepts `IReadOnlyList<PapercraftMeshData>` to lay out several meshes on shared
pages with continuous label numbering. `PapercraftMeshData.FromMesh(mesh, matrix)` bakes a
transform (and fixes winding on a mirrored matrix).

### Progress + cancellation

`PapercraftExporter.Export` runs synchronously. For a progress bar and a cancel button, drive a
`PapercraftJob` incrementally instead — each `MoveNext` on `Run()` does one chunk of work (one
mesh, one page, or the final render) and updates `Progress` (0..1) and `Status`. Cancel by simply
not enumerating further; `Result` stays `null` until the job finishes.

```csharp
var job   = new PapercraftJob(meshes, options);
var steps = job.Run().GetEnumerator();
while (steps.MoveNext())
{
    ShowProgress(job.Progress, job.Status);
    if (cancelled) { break; }   // Result stays null
}
if (job.IsDone) { PapercraftFiles.Write(job.Result, path); }
```

`PapercraftFiles.Write(result, pdfPath)` writes the PDF plus one SVG per page beside it.

### Entry points in this project

- **Runtime control panel** (`DiffuserControlPanel`): the EXPORT section has an *Export papercraft*
  button, a `ProgressBar`, and a *Cancel* button. The panel runs the job as a time-sliced
  coroutine so the bar repaints and Cancel stays responsive on large grids; it saves via an editor
  Save-File dialog in Play mode and to `Application.persistentDataPath/Papercraft/` in a build.
- **Editor context menu**: `DiffuserGrid` ▸ **Export Papercraft** (synchronous, Save-File dialog).

Both feed `DiffuserGrid.CollectPapercraftMeshes()`, which bakes every block mesh into grid-local
space.

## Tests

EditMode tests live in `Assets/Tests/EditMode` (`DiffuserCreator.Papercraft.Tests` asmdef):
dual-graph construction, fold/cut split, unfold isometry, overlap detection on a plain cube and a
perturbed cube, tab placement, and SVG/PDF output. Run via the Unity Test Runner.

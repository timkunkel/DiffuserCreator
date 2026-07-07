using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // Runs the export pipeline in small chunks so a coroutine can drive it, repaint a progress bar
    // between chunks, and cancel by simply stopping enumeration. Each MoveNext on Run() does one
    // unit of work (one mesh, one page, or the final render) and updates Progress/Status.
    // PapercraftExporter.Export just enumerates this to completion synchronously.
    //
    // The pipeline unfolds every mesh first (in model units), then picks a single scale — either
    // the fixed MillimetersPerModelUnit or, when FitSinglePieceToPage is set, one computed so the
    // largest block fits a page — and applies that same scale to every block before drawing.
    public class PapercraftJob
    {
        private const float UNFOLD_PHASE_END = 0.45f;
        private const float DRAW_PHASE_END   = 0.80f;
        private const float PACK_PROGRESS    = 0.82f;
        private const float RENDER_PHASE_END = 0.97f;

        private readonly IReadOnlyList<PapercraftMeshData> _meshes;
        private readonly PapercraftOptions                 _options;

        public float            Progress { get; private set; }
        public string           Status   { get; private set; } = "Preparing";
        public bool             IsDone   { get; private set; }
        public PapercraftResult Result   { get; private set; }

        public PapercraftJob(IReadOnlyList<PapercraftMeshData> meshes, PapercraftOptions options = null)
        {
            _meshes  = meshes;
            _options = options ?? new PapercraftOptions();
        }

        private sealed class UnfoldedMesh
        {
            public PapercraftModel     Model;
            public bool[]              FoldEdges;
            public List<UnfoldedPiece> Pieces;
        }

        public IEnumerable Run()
        {
            int meshCount  = Mathf.Max(1, _meshes.Count);
            int pieceCount = 0;
            int splitCount = 0;

            // Phase 1 — unfold every mesh in model units.
            var unfolded = new List<UnfoldedMesh>();
            for (int m = 0; m < _meshes.Count; m++)
            {
                Status = _meshes.Count > 1 ? $"Unfolding block {m + 1}/{_meshes.Count}" : "Unfolding block";

                PapercraftModel model = PapercraftModel.Build(
                    _meshes[m].Vertices, _meshes[m].Triangles, _options.WeldTolerance, _options.CoplanarToleranceDeg);
                bool[]              foldEdges = SpanningTreeBuilder.BuildFoldEdges(model);
                List<UnfoldedPiece> pieces    = OverlapResolver.Resolve(model, foldEdges, out int splits);

                splitCount += splits;
                pieceCount += pieces.Count;
                unfolded.Add(new UnfoldedMesh { Model = model, FoldEdges = foldEdges, Pieces = pieces });

                Progress = UNFOLD_PHASE_END * (m + 1) / meshCount;
                yield return null;
            }

            float scale = ResolveScale(unfolded);

            // Phase 2 — scale by the shared value, add tabs, and build drawings.
            var drawings  = new List<PieceDrawing>();
            int nextLabel = 1;
            for (int m = 0; m < unfolded.Count; m++)
            {
                Status = unfolded.Count > 1 ? $"Laying out block {m + 1}/{unfolded.Count}" : "Laying out block";
                BuildDrawings(unfolded[m], scale, drawings, ref nextLabel);

                Progress = UNFOLD_PHASE_END + (DRAW_PHASE_END - UNFOLD_PHASE_END) * (m + 1) / meshCount;
                yield return null;
            }

            Status = "Packing pages";
            List<PapercraftPage> pages = PageLayout.Pack(drawings, _options);
            Progress = PACK_PROGRESS;
            yield return null;

            var svgPages  = new string[pages.Count];
            int pageCount = Mathf.Max(1, pages.Count);
            for (int i = 0; i < pages.Count; i++)
            {
                Status      = pages.Count > 1 ? $"Rendering page {i + 1}/{pages.Count}" : "Rendering page";
                svgPages[i] = SvgRenderer.Render(pages[i]);
                Progress    = PACK_PROGRESS + (RENDER_PHASE_END - PACK_PROGRESS) * (i + 1) / pageCount;
                yield return null;
            }

            Status = "Writing PDF";
            byte[] pdf = PdfRenderer.Render(pages);

            Result = new PapercraftResult
            {
                Pages                   = pages,
                SvgPages                = svgPages,
                PdfBytes                = pdf,
                PieceCount              = pieceCount,
                OverlapSplitCount       = splitCount,
                AppliedScaleMmPerUnit   = scale
            };

            Progress = 1f;
            Status   = "Done";
            IsDone   = true;
            yield return null;
        }

        // The scale (mm per model unit) applied to every block. Either the fixed value or, when
        // fitting to the page, the largest single-block net scaled to the printable area minus a
        // glue-tab allowance, so each block fits on one sheet and all blocks stay proportional.
        private float ResolveScale(List<UnfoldedMesh> unfolded)
        {
            if (!_options.FitSinglePieceToPage) { return _options.MillimetersPerModelUnit; }

            float maxWidth  = 0f;
            float maxHeight = 0f;
            foreach (UnfoldedMesh mesh in unfolded)
            {
                foreach (UnfoldedPiece piece in mesh.Pieces)
                {
                    Rect bounds = Unfolder.CalculateBounds(piece);
                    maxWidth  = Mathf.Max(maxWidth, bounds.width);
                    maxHeight = Mathf.Max(maxHeight, bounds.height);
                }
            }

            if (maxWidth <= 0f || maxHeight <= 0f) { return _options.MillimetersPerModelUnit; }

            float allowance      = 2f * _options.TabHeightMm;
            float availableWidth  = Mathf.Max(1f, _options.PageSizeMm.x - 2f * _options.PageMarginMm - allowance);
            float availableHeight = Mathf.Max(1f, _options.PageSizeMm.y - 2f * _options.PageMarginMm - allowance);

            return Mathf.Min(availableWidth / maxWidth, availableHeight / maxHeight);
        }

        private void BuildDrawings(UnfoldedMesh mesh, float scale, List<PieceDrawing> drawings, ref int nextLabel)
        {
            foreach (UnfoldedPiece piece in mesh.Pieces)
            {
                piece.Scale(scale);
            }

            List<GlueTab>        tabs         = TabGenerator.CreateTabs(mesh.Model, mesh.Pieces, mesh.FoldEdges, _options);
            Dictionary<int, int> labelNumbers = AssignLabelNumbers(mesh.Model, mesh.FoldEdges, ref nextLabel);
            var                  tabsByPiece  = GroupTabsByPiece(mesh.Pieces, tabs);

            foreach (UnfoldedPiece piece in mesh.Pieces)
            {
                tabsByPiece.TryGetValue(piece, out List<GlueTab> pieceTabs);
                drawings.Add(PieceDrawing.Create(
                    mesh.Model, piece, pieceTabs ?? new List<GlueTab>(), mesh.FoldEdges, labelNumbers, _options));
            }
        }

        private static Dictionary<int, int> AssignLabelNumbers(PapercraftModel model, bool[] foldEdges, ref int nextLabelNumber)
        {
            var labelNumbers = new Dictionary<int, int>();
            for (int e = 0; e < model.Edges.Length; e++)
            {
                if (model.Edges[e].IsInterior && !foldEdges[e])
                {
                    labelNumbers.Add(e, nextLabelNumber++);
                }
            }

            return labelNumbers;
        }

        private static Dictionary<UnfoldedPiece, List<GlueTab>> GroupTabsByPiece(List<UnfoldedPiece> pieces, List<GlueTab> tabs)
        {
            var pieceByFace = new Dictionary<UnfoldedFace, UnfoldedPiece>();
            foreach (UnfoldedPiece piece in pieces)
            {
                foreach (UnfoldedFace face in piece.Faces)
                {
                    pieceByFace.Add(face, piece);
                }
            }

            var tabsByPiece = new Dictionary<UnfoldedPiece, List<GlueTab>>();
            foreach (GlueTab tab in tabs)
            {
                UnfoldedPiece piece = pieceByFace[tab.OwnerFace];
                if (!tabsByPiece.TryGetValue(piece, out List<GlueTab> list))
                {
                    list = new List<GlueTab>();
                    tabsByPiece.Add(piece, list);
                }
                list.Add(tab);
            }

            return tabsByPiece;
        }
    }
}

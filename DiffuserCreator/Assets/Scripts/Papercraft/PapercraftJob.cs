using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // Runs the export pipeline in small chunks so a coroutine can drive it, repaint a progress bar
    // between chunks, and cancel by simply stopping enumeration. Each MoveNext on Run() does one
    // unit of work (one mesh, one page, or a final render step) and updates Progress/Status.
    // PapercraftExporter.Export just enumerates this to completion synchronously.
    public class PapercraftJob
    {
        private const float MESH_PHASE_FRACTION   = 0.8f;
        private const float RENDER_PHASE_FRACTION = 0.18f;

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

        public IEnumerable Run()
        {
            var drawings   = new List<PieceDrawing>();
            int nextLabel  = 1;
            int pieceCount = 0;
            int splitCount = 0;

            int meshCount = Mathf.Max(1, _meshes.Count);
            for (int m = 0; m < _meshes.Count; m++)
            {
                Status = _meshes.Count > 1 ? $"Unfolding mesh {m + 1}/{_meshes.Count}" : "Unfolding mesh";
                ProcessMesh(_meshes[m], drawings, ref nextLabel, ref pieceCount, ref splitCount);
                Progress = MESH_PHASE_FRACTION * (m + 1) / meshCount;
                yield return null;
            }

            Status = "Packing pages";
            List<PapercraftPage> pages = PageLayout.Pack(drawings, _options);
            Progress = MESH_PHASE_FRACTION;
            yield return null;

            var svgPages = new string[pages.Count];
            int pageCount = Mathf.Max(1, pages.Count);
            for (int i = 0; i < pages.Count; i++)
            {
                Status      = pages.Count > 1 ? $"Rendering page {i + 1}/{pages.Count}" : "Rendering page";
                svgPages[i] = SvgRenderer.Render(pages[i]);
                Progress    = MESH_PHASE_FRACTION + RENDER_PHASE_FRACTION * (i + 1) / pageCount;
                yield return null;
            }

            Status = "Writing PDF";
            byte[] pdf = PdfRenderer.Render(pages);

            Result = new PapercraftResult
            {
                Pages             = pages,
                SvgPages          = svgPages,
                PdfBytes          = pdf,
                PieceCount        = pieceCount,
                OverlapSplitCount = splitCount
            };

            Progress = 1f;
            Status   = "Done";
            IsDone   = true;
            yield return null;
        }

        private void ProcessMesh(
            PapercraftMeshData meshData,
            List<PieceDrawing> drawings,
            ref int            nextLabel,
            ref int            pieceCount,
            ref int            splitCount)
        {
            PapercraftModel model = PapercraftModel.Build(
                meshData.Vertices, meshData.Triangles, _options.WeldTolerance, _options.CoplanarToleranceDeg);

            bool[]              foldEdges = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces    = OverlapResolver.Resolve(model, foldEdges, out int splits);
            splitCount += splits;
            pieceCount += pieces.Count;

            foreach (UnfoldedPiece piece in pieces)
            {
                piece.Scale(_options.MillimetersPerModelUnit);
            }

            List<GlueTab>        tabs         = TabGenerator.CreateTabs(model, pieces, foldEdges, _options);
            Dictionary<int, int> labelNumbers = AssignLabelNumbers(model, foldEdges, ref nextLabel);
            var                  tabsByPiece  = GroupTabsByPiece(pieces, tabs);

            foreach (UnfoldedPiece piece in pieces)
            {
                tabsByPiece.TryGetValue(piece, out List<GlueTab> pieceTabs);
                drawings.Add(PieceDrawing.Create(
                    model, piece, pieceTabs ?? new List<GlueTab>(), foldEdges, labelNumbers, _options));
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

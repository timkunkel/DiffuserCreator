using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public struct PapercraftMeshData
    {
        public Vector3[] Vertices;
        public int[]     Triangles;

        public static PapercraftMeshData FromMesh(Mesh mesh)
        {
            return FromMesh(mesh, Matrix4x4.identity);
        }

        public static PapercraftMeshData FromMesh(Mesh mesh, Matrix4x4 transform)
        {
            Vector3[] source   = mesh.vertices;
            var       vertices = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                vertices[i] = transform.MultiplyPoint3x4(source[i]);
            }

            int[] triangles = (int[])mesh.triangles.Clone();
            if (transform.determinant < 0f)
            {
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                }
            }

            return new PapercraftMeshData { Vertices = vertices, Triangles = triangles };
        }
    }

    public class PapercraftResult
    {
        public List<PapercraftPage> Pages;
        public string[]             SvgPages;
        public byte[]               PdfBytes;
        public int                  PieceCount;
        public int                  OverlapSplitCount;
    }

    // Facade over the whole pipeline: welded polygon model -> dual-graph spanning tree ->
    // tree unfolding -> overlap safety cuts -> glue tabs -> page layout -> SVG + PDF pages.
    public static class PapercraftExporter
    {
        public static PapercraftResult Export(Mesh mesh, PapercraftOptions options = null)
        {
            return Export(new[] { PapercraftMeshData.FromMesh(mesh) }, options);
        }

        public static PapercraftResult Export(IReadOnlyList<PapercraftMeshData> meshes, PapercraftOptions options = null)
        {
            options = options ?? new PapercraftOptions();

            var drawings        = new List<PieceDrawing>();
            int nextLabelNumber = 1;
            int pieceCount      = 0;
            int splitCount      = 0;

            foreach (PapercraftMeshData meshData in meshes)
            {
                PapercraftModel model = PapercraftModel.Build(
                    meshData.Vertices, meshData.Triangles, options.WeldTolerance, options.CoplanarToleranceDeg);

                bool[]              foldEdges = SpanningTreeBuilder.BuildFoldEdges(model);
                List<UnfoldedPiece> pieces    = OverlapResolver.Resolve(model, foldEdges, out int splits);
                splitCount += splits;
                pieceCount += pieces.Count;

                foreach (UnfoldedPiece piece in pieces)
                {
                    piece.Scale(options.MillimetersPerModelUnit);
                }

                List<GlueTab>        tabs         = TabGenerator.CreateTabs(model, pieces, foldEdges, options);
                Dictionary<int, int> labelNumbers = AssignLabelNumbers(model, foldEdges, ref nextLabelNumber);
                var                  tabsByPiece  = GroupTabsByPiece(pieces, tabs);

                foreach (UnfoldedPiece piece in pieces)
                {
                    tabsByPiece.TryGetValue(piece, out List<GlueTab> pieceTabs);
                    drawings.Add(PieceDrawing.Create(
                        model, piece, pieceTabs ?? new List<GlueTab>(), foldEdges, labelNumbers, options));
                }
            }

            List<PapercraftPage> pages = PageLayout.Pack(drawings, options);

            var svgPages = new string[pages.Count];
            for (int i = 0; i < pages.Count; i++)
            {
                svgPages[i] = SvgRenderer.Render(pages[i]);
            }

            return new PapercraftResult
            {
                Pages             = pages,
                SvgPages          = svgPages,
                PdfBytes          = PdfRenderer.Render(pages),
                PieceCount        = pieceCount,
                OverlapSplitCount = splitCount
            };
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

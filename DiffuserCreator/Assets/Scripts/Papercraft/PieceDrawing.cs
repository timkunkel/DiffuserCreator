using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // Turns one unfolded piece plus its tabs into drawable primitives in piece-local millimeters
    // (y up): solid cut lines, dashed fold lines (tab bases included, since tabs fold behind the
    // partner edge), and the matching glue-pair labels on both sides of every cut edge.
    public class PieceDrawing
    {
        public readonly List<PapercraftPolyline> Polylines = new List<PapercraftPolyline>();
        public readonly List<PapercraftLabel>    Labels    = new List<PapercraftLabel>();

        public Rect Bounds;

        public static PieceDrawing Create(
            PapercraftModel               model,
            UnfoldedPiece                 piece,
            IReadOnlyList<GlueTab>        tabs,
            bool[]                        edgeIsFold,
            IReadOnlyDictionary<int, int> labelNumbers,
            PapercraftOptions             options)
        {
            var drawing   = new PieceDrawing();
            var tabByEdge = new Dictionary<(UnfoldedFace, int), GlueTab>();
            foreach (GlueTab tab in tabs)
            {
                tabByEdge[(tab.OwnerFace, tab.EdgeIndex)] = tab;
            }

            foreach (UnfoldedFace face in piece.Faces)
            {
                drawing.AddFaceEdges(model, face, tabByEdge, edgeIsFold, labelNumbers, options);
            }

            drawing.Bounds = drawing.CalculateBounds();
            return drawing;
        }

        private void AddFaceEdges(
            PapercraftModel                          model,
            UnfoldedFace                             face,
            Dictionary<(UnfoldedFace, int), GlueTab> tabByEdge,
            bool[]                                   edgeIsFold,
            IReadOnlyDictionary<int, int>            labelNumbers,
            PapercraftOptions                        options)
        {
            PapercraftFace modelFace = model.Faces[face.FaceIndex];

            for (int i = 0; i < modelFace.EdgeIndices.Length; i++)
            {
                int            e    = modelFace.EdgeIndices[i];
                PapercraftEdge edge = model.Edges[e];
                Vector2        a    = face.Outline[i];
                Vector2        b    = face.Outline[(i + 1) % face.Outline.Length];

                if (edge.IsInterior && edgeIsFold[e])
                {
                    // Both faces of a fold edge sit in the same piece with coinciding segments;
                    // draw the line only from FaceA to avoid doubled strokes.
                    if (edge.FaceA == face.FaceIndex)
                    {
                        AddLine(LineKind.Fold, a, b);
                    }
                    continue;
                }

                labelNumbers.TryGetValue(e, out int labelNumber);

                if (tabByEdge.TryGetValue((face, e), out GlueTab tab))
                {
                    Polylines.Add(new PapercraftPolyline { Points = tab.Outline, Kind = LineKind.Cut });
                    AddLine(LineKind.Fold, a, b);
                    if (labelNumber > 0)
                    {
                        AddLabel(Polygon2D.Centroid(tab.Outline), labelNumber, options);
                    }
                }
                else
                {
                    AddLine(LineKind.Cut, a, b);
                    if (labelNumber > 0)
                    {
                        Vector2 direction = (b - a).normalized;
                        Vector2 inward    = new Vector2(-direction.y, direction.x);
                        AddLabel((a + b) * 0.5f + inward * options.LabelHeightMm, labelNumber, options);
                    }
                }
            }
        }

        private void AddLine(LineKind kind, Vector2 a, Vector2 b)
        {
            Polylines.Add(new PapercraftPolyline { Points = new[] { a, b }, Kind = kind });
        }

        private void AddLabel(Vector2 position, int number, PapercraftOptions options)
        {
            Labels.Add(new PapercraftLabel
            {
                Position = position,
                Text     = number.ToString(),
                HeightMm = options.LabelHeightMm
            });
        }

        private Rect CalculateBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (PapercraftPolyline polyline in Polylines)
            {
                foreach (Vector2 point in polyline.Points)
                {
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }
            }

            const float padding = 0.5f;
            return Rect.MinMaxRect(minX - padding, minY - padding, maxX + padding, maxY + padding);
        }
    }
}

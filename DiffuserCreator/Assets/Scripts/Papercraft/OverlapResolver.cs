using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // Safety net for meshes whose spanning tree unfolds onto itself: detects overlapping face
    // pairs and re-cuts the fold edge that attached the later-placed face, splitting the net into
    // more pieces until it lies flat. Simple box-like meshes should never trigger this.
    public static class OverlapResolver
    {
        public static List<UnfoldedPiece> Resolve(PapercraftModel model, bool[] edgeIsFold, out int splitCount)
        {
            splitCount = 0;

            for (int iteration = 0; iteration <= model.Edges.Length; iteration++)
            {
                List<UnfoldedPiece> pieces  = Unfolder.Unfold(model, edgeIsFold);
                int                 cutEdge = FindOverlapCutEdge(pieces);
                if (cutEdge < 0) { return pieces; }

                Debug.LogWarning($"Papercraft: unfolded faces overlap, converting fold edge {cutEdge} to a cut and re-unfolding.");
                edgeIsFold[cutEdge] = false;
                splitCount++;
            }

            Debug.LogError("Papercraft: overlap resolution did not converge.");
            return Unfolder.Unfold(model, edgeIsFold);
        }

        private static int FindOverlapCutEdge(List<UnfoldedPiece> pieces)
        {
            foreach (UnfoldedPiece piece in pieces)
            {
                for (int i = 0; i < piece.Faces.Count; i++)
                {
                    for (int j = i + 1; j < piece.Faces.Count; j++)
                    {
                        if (!Polygon2D.Overlaps(piece.Faces[i].Outline, piece.Faces[j].Outline)) { continue; }

                        // Faces are stored in placement (BFS) order, so the later face always has
                        // a parent fold edge that can be detached.
                        return piece.Faces[j].ParentEdgeIndex;
                    }
                }
            }

            return -1;
        }
    }
}

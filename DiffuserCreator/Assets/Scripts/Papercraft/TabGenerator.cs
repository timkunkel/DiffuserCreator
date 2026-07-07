using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public class GlueTab
    {
        public int          EdgeIndex;
        public UnfoldedFace OwnerFace;
        public Vector2[]    Outline;
    }

    // Puts a trapezoidal glue tab on exactly one side of every interior cut edge. The tab tapers
    // inward from the edge so it tucks behind the neighboring piece when folded; if it would land
    // on top of nearby geometry it flips to the other side, and as a last resort shrinks.
    public static class TabGenerator
    {
        public static List<GlueTab> CreateTabs(
            PapercraftModel     model,
            List<UnfoldedPiece> pieces,
            bool[]              edgeIsFold,
            PapercraftOptions   options)
        {
            var tabs           = new List<GlueTab>();
            var pieceByFace    = new UnfoldedPiece[model.Faces.Length];
            var instanceByFace = new UnfoldedFace[model.Faces.Length];

            foreach (UnfoldedPiece piece in pieces)
            {
                foreach (UnfoldedFace face in piece.Faces)
                {
                    pieceByFace[face.FaceIndex]    = piece;
                    instanceByFace[face.FaceIndex] = face;
                }
            }

            for (int e = 0; e < model.Edges.Length; e++)
            {
                PapercraftEdge edge = model.Edges[e];
                if (!edge.IsInterior || edgeIsFold[e]) { continue; }

                GlueTab tab = PlaceTab(model, e, edge.FaceA, edge.FaceB, pieceByFace, instanceByFace, options);
                tabs.Add(tab);
            }

            return tabs;
        }

        private static GlueTab PlaceTab(
            PapercraftModel   model,
            int               edgeIndex,
            int               firstFace,
            int               secondFace,
            UnfoldedPiece[]   pieceByFace,
            UnfoldedFace[]    instanceByFace,
            PapercraftOptions options)
        {
            GlueTab tab = BuildTab(model, edgeIndex, instanceByFace[firstFace], options.TabHeightMm, options);
            if (!OverlapsPiece(pieceByFace[firstFace], tab.Outline)) { return tab; }

            GlueTab flipped = BuildTab(model, edgeIndex, instanceByFace[secondFace], options.TabHeightMm, options);
            if (!OverlapsPiece(pieceByFace[secondFace], flipped.Outline)) { return flipped; }

            Debug.LogWarning($"Papercraft: glue tab on edge {edgeIndex} collides on both sides, shrinking it.");
            return BuildTab(model, edgeIndex, instanceByFace[firstFace], options.TabHeightMm * 0.5f, options);
        }

        private static GlueTab BuildTab(
            PapercraftModel   model,
            int               edgeIndex,
            UnfoldedFace      face,
            float             heightMm,
            PapercraftOptions options)
        {
            PapercraftFace modelFace = model.Faces[face.FaceIndex];
            int            loop      = Array.IndexOf(modelFace.EdgeIndices, edgeIndex);

            Vector2 a         = face.Outline[loop];
            Vector2 b         = face.Outline[(loop + 1) % face.Outline.Length];
            Vector2 direction = (b - a).normalized;
            Vector2 outward   = new Vector2(direction.y, -direction.x);
            float   length    = (b - a).magnitude;

            float height = Mathf.Min(heightMm, length * 0.45f);
            float inset  = Mathf.Min(height / Mathf.Tan(options.TabShoulderAngleDeg * Mathf.Deg2Rad), length * 0.25f);

            return new GlueTab
            {
                EdgeIndex = edgeIndex,
                OwnerFace = face,
                Outline   = new[]
                {
                    a,
                    a + outward * height + direction * inset,
                    b + outward * height - direction * inset,
                    b
                }
            };
        }

        private static bool OverlapsPiece(UnfoldedPiece piece, Vector2[] tabOutline)
        {
            foreach (UnfoldedFace face in piece.Faces)
            {
                if (Polygon2D.Overlaps(face.Outline, tabOutline)) { return true; }
            }

            return false;
        }
    }
}

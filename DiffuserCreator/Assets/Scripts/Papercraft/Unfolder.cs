using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public class UnfoldedFace
    {
        public int       FaceIndex;
        public int       ParentEdgeIndex;
        public Vector2[] Outline;
    }

    public class UnfoldedPiece
    {
        public readonly List<UnfoldedFace> Faces = new List<UnfoldedFace>();

        public void Scale(float factor)
        {
            foreach (UnfoldedFace face in Faces)
            {
                for (int i = 0; i < face.Outline.Length; i++)
                {
                    face.Outline[i] *= factor;
                }
            }
        }
    }

    // Flattens every fold-connected component into the plane by walking the spanning tree and
    // rigidly placing each child face against its parent across their shared edge. Windings stay
    // counter-clockwise throughout, so the printed side is the model's outside surface and no
    // reflections are ever needed.
    public static class Unfolder
    {
        public static List<UnfoldedPiece> Unfold(PapercraftModel model, bool[] edgeIsFold)
        {
            var pieces  = new List<UnfoldedPiece>();
            var visited = new bool[model.Faces.Length];
            var placed  = new UnfoldedFace[model.Faces.Length];

            for (int root = 0; root < model.Faces.Length; root++)
            {
                if (visited[root]) { continue; }

                var piece = new UnfoldedPiece();
                placed[root] = new UnfoldedFace
                {
                    FaceIndex       = root,
                    ParentEdgeIndex = -1,
                    Outline         = LocalOutline(model, root)
                };
                piece.Faces.Add(placed[root]);
                visited[root] = true;

                var queue = new Queue<int>();
                queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    int            faceIndex = queue.Dequeue();
                    PapercraftFace face      = model.Faces[faceIndex];

                    foreach (int e in face.EdgeIndices)
                    {
                        PapercraftEdge edge = model.Edges[e];
                        if (!edge.IsInterior || !edgeIsFold[e]) { continue; }

                        int neighbor = edge.FaceA == faceIndex ? edge.FaceB : edge.FaceA;
                        if (visited[neighbor]) { continue; }

                        placed[neighbor] = PlaceAgainstParent(model, neighbor, e, placed[faceIndex]);
                        piece.Faces.Add(placed[neighbor]);
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                pieces.Add(piece);
            }

            return pieces;
        }

        public static Rect CalculateBounds(UnfoldedPiece piece)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (UnfoldedFace face in piece.Faces)
            {
                foreach (Vector2 point in face.Outline)
                {
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        // Projects the face into its own plane: x along the first edge, y = normal x u, which keeps
        // a counter-clockwise loop counter-clockwise when viewed from the outward-normal side.
        private static Vector2[] LocalOutline(PapercraftModel model, int faceIndex)
        {
            PapercraftFace face   = model.Faces[faceIndex];
            Vector3        origin = model.Vertices[face.VertexIndices[0]];
            Vector3        u      = (model.Vertices[face.VertexIndices[1]] - origin).normalized;
            Vector3        w      = Vector3.Cross(face.Normal, u).normalized;

            var outline = new Vector2[face.VertexIndices.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 offset = model.Vertices[face.VertexIndices[i]] - origin;
                outline[i] = new Vector2(Vector3.Dot(offset, u), Vector3.Dot(offset, w));
            }

            return outline;
        }

        private static UnfoldedFace PlaceAgainstParent(PapercraftModel model, int faceIndex, int edgeIndex, UnfoldedFace parent)
        {
            PapercraftFace parentFace = model.Faces[parent.FaceIndex];
            PapercraftFace childFace  = model.Faces[faceIndex];

            int parentLoop = System.Array.IndexOf(parentFace.EdgeIndices, edgeIndex);
            int childLoop  = System.Array.IndexOf(childFace.EdgeIndices, edgeIndex);
            int parentNext = (parentLoop + 1) % parentFace.VertexIndices.Length;
            int childNext  = (childLoop + 1) % childFace.VertexIndices.Length;

            Vector2[] local = LocalOutline(model, faceIndex);

            int     childVertexA = childFace.VertexIndices[childLoop];
            bool    sameOrder    = parentFace.VertexIndices[parentLoop] == childVertexA;
            Vector2 targetA      = sameOrder ? parent.Outline[parentLoop] : parent.Outline[parentNext];
            Vector2 targetB      = sameOrder ? parent.Outline[parentNext] : parent.Outline[parentLoop];

            Vector2 fromA = local[childLoop];
            Vector2 d1    = local[childNext] - fromA;
            Vector2 d2    = targetB - targetA;

            float scale = d1.magnitude * d2.magnitude;
            float cos   = Vector2.Dot(d1, d2) / scale;
            float sin   = (d1.x * d2.y - d1.y * d2.x) / scale;

            var outline = new Vector2[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                Vector2 p  = local[i] - fromA;
                outline[i] = new Vector2(cos * p.x - sin * p.y, sin * p.x + cos * p.y) + targetA;
            }

            return new UnfoldedFace { FaceIndex = faceIndex, ParentEdgeIndex = edgeIndex, Outline = outline };
        }
    }
}

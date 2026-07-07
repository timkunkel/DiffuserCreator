using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public class PapercraftFace
    {
        public int[]   VertexIndices;
        public int[]   EdgeIndices;
        public Vector3 Normal;
    }

    public class PapercraftEdge
    {
        public int   VertexA;
        public int   VertexB;
        public int   FaceA = -1;
        public int   FaceB = -1;
        public float DihedralAngleDeg;

        public bool IsInterior => FaceB >= 0;
    }

    // Welded polygon-face view of a triangle mesh plus its face-adjacency (dual) graph. Coplanar
    // neighboring triangles are merged into single polygon faces so a box unfolds into six quads
    // instead of twelve triangles; non-planar regions (like a perturbed front face) stay triangles.
    public class PapercraftModel
    {
        public Vector3[]        Vertices;
        public PapercraftFace[] Faces;
        public PapercraftEdge[] Edges;

        #region Construction

        public static PapercraftModel Build(
            Vector3[] vertices,
            int[]     triangles,
            float     weldTolerance        = 1e-4f,
            float     coplanarToleranceDeg = 0.5f)
        {
            int[]     remap  = WeldVertices(vertices, weldTolerance, out Vector3[] welded);
            List<int[]> tris = CollectTriangles(triangles, remap, welded);

            int[] regionOf = MergeCoplanarRegions(tris, welded, coplanarToleranceDeg);

            var model = new PapercraftModel { Vertices = welded };
            model.Faces = BuildFaces(tris, regionOf, welded);
            model.BuildEdges();
            return model;
        }

        private static int[] WeldVertices(Vector3[] vertices, float tolerance, out Vector3[] welded)
        {
            var indexByCell = new Dictionary<Vector3Int, int>();
            var points      = new List<Vector3>();
            var remap       = new int[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v    = vertices[i];
                var     cell = new Vector3Int(
                    Mathf.RoundToInt(v.x / tolerance),
                    Mathf.RoundToInt(v.y / tolerance),
                    Mathf.RoundToInt(v.z / tolerance));

                if (!indexByCell.TryGetValue(cell, out int index))
                {
                    index = points.Count;
                    points.Add(v);
                    indexByCell.Add(cell, index);
                }

                remap[i] = index;
            }

            welded = points.ToArray();
            return remap;
        }

        private static List<int[]> CollectTriangles(int[] triangles, int[] remap, Vector3[] welded)
        {
            var tris = new List<int[]>();

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = remap[triangles[t]];
                int b = remap[triangles[t + 1]];
                int c = remap[triangles[t + 2]];

                if (a == b || b == c || c == a) { continue; }

                Vector3 cross = Vector3.Cross(welded[b] - welded[a], welded[c] - welded[a]);
                if (cross.sqrMagnitude < 1e-12f) { continue; }

                tris.Add(new[] { a, b, c });
            }

            return tris;
        }

        private static int[] MergeCoplanarRegions(List<int[]> tris, Vector3[] welded, float coplanarToleranceDeg)
        {
            var normals = new Vector3[tris.Count];
            for (int t = 0; t < tris.Count; t++)
            {
                int[] tri  = tris[t];
                normals[t] = Vector3.Cross(welded[tri[1]] - welded[tri[0]], welded[tri[2]] - welded[tri[0]]).normalized;
            }

            var trisByEdge = new Dictionary<(int, int), List<int>>();
            for (int t = 0; t < tris.Count; t++)
            {
                int[] tri = tris[t];
                for (int i = 0; i < 3; i++)
                {
                    (int, int) key = UndirectedKey(tri[i], tri[(i + 1) % 3]);
                    if (!trisByEdge.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        trisByEdge.Add(key, list);
                    }
                    list.Add(t);
                }
            }

            var   parent = new int[tris.Count];
            for (int t = 0; t < tris.Count; t++) { parent[t] = t; }

            float cosTolerance = Mathf.Cos(coplanarToleranceDeg * Mathf.Deg2Rad);
            foreach (List<int> sharing in trisByEdge.Values)
            {
                if (sharing.Count != 2) { continue; }
                if (Vector3.Dot(normals[sharing[0]], normals[sharing[1]]) >= cosTolerance)
                {
                    Union(parent, sharing[0], sharing[1]);
                }
            }

            var regionOf = new int[tris.Count];
            for (int t = 0; t < tris.Count; t++) { regionOf[t] = Find(parent, t); }
            return regionOf;
        }

        private static PapercraftFace[] BuildFaces(List<int[]> tris, int[] regionOf, Vector3[] welded)
        {
            var trisByRegion = new Dictionary<int, List<int[]>>();
            for (int t = 0; t < tris.Count; t++)
            {
                if (!trisByRegion.TryGetValue(regionOf[t], out List<int[]> list))
                {
                    list = new List<int[]>();
                    trisByRegion.Add(regionOf[t], list);
                }
                list.Add(tris[t]);
            }

            var faces = new List<PapercraftFace>();
            foreach (List<int[]> region in trisByRegion.Values)
            {
                int[] loop = region.Count == 1 ? region[0] : ExtractBoundaryLoop(region);
                if (loop != null)
                {
                    faces.Add(MakeFace(loop, welded));
                }
                else
                {
                    foreach (int[] tri in region)
                    {
                        faces.Add(MakeFace(tri, welded));
                    }
                }
            }

            return faces.ToArray();
        }

        // Directed edges of consistently wound triangles cancel pairwise inside a region, so the
        // remaining ones chain into the region's outer boundary. Returns null when the region does
        // not form a single simple loop (holes, non-manifold junk) — callers fall back to raw triangles.
        private static int[] ExtractBoundaryLoop(List<int[]> region)
        {
            var directed = new HashSet<(int, int)>();
            foreach (int[] tri in region)
            {
                for (int i = 0; i < 3; i++)
                {
                    directed.Add((tri[i], tri[(i + 1) % 3]));
                }
            }

            var next  = new Dictionary<int, int>();
            int start = int.MaxValue;
            foreach ((int a, int b) in directed)
            {
                if (directed.Contains((b, a))) { continue; }
                if (next.ContainsKey(a)) { return null; }
                next.Add(a, b);
                start = Mathf.Min(start, a);
            }

            if (next.Count < 3) { return null; }

            var loop    = new List<int>();
            int current = start;
            do
            {
                loop.Add(current);
                if (!next.TryGetValue(current, out current)) { return null; }
            } while (current != start && loop.Count <= next.Count);

            return loop.Count == next.Count ? loop.ToArray() : null;
        }

        private static PapercraftFace MakeFace(int[] loop, Vector3[] welded)
        {
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < loop.Length; i++)
            {
                normal += Vector3.Cross(welded[loop[i]], welded[loop[(i + 1) % loop.Length]]);
            }

            return new PapercraftFace { VertexIndices = loop, Normal = normal.normalized };
        }

        private void BuildEdges()
        {
            var edges       = new List<PapercraftEdge>();
            var edgeByKey   = new Dictionary<(int, int), int>();

            for (int f = 0; f < Faces.Length; f++)
            {
                PapercraftFace face = Faces[f];
                face.EdgeIndices = new int[face.VertexIndices.Length];

                for (int i = 0; i < face.VertexIndices.Length; i++)
                {
                    int a = face.VertexIndices[i];
                    int b = face.VertexIndices[(i + 1) % face.VertexIndices.Length];

                    (int, int) key = UndirectedKey(a, b);
                    if (!edgeByKey.TryGetValue(key, out int edgeIndex))
                    {
                        edgeIndex = edges.Count;
                        edges.Add(new PapercraftEdge { VertexA = key.Item1, VertexB = key.Item2 });
                        edgeByKey.Add(key, edgeIndex);
                    }

                    PapercraftEdge edge = edges[edgeIndex];
                    if (edge.FaceA < 0)
                    {
                        edge.FaceA = f;
                    }
                    else if (edge.FaceB < 0)
                    {
                        edge.FaceB = f;
                    }
                    else
                    {
                        Debug.LogWarning($"Papercraft: non-manifold edge {edge.VertexA}-{edge.VertexB} shared by more than two faces, extra adjacency ignored.");
                    }

                    face.EdgeIndices[i] = edgeIndex;
                }
            }

            foreach (PapercraftEdge edge in edges)
            {
                if (edge.IsInterior)
                {
                    edge.DihedralAngleDeg = Vector3.Angle(Faces[edge.FaceA].Normal, Faces[edge.FaceB].Normal);
                }
            }

            Edges = edges.ToArray();
        }

        #endregion

        #region Helpers

        private static (int, int) UndirectedKey(int a, int b)
        {
            return a < b ? (a, b) : (b, a);
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i         = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            parent[Find(parent, a)] = Find(parent, b);
        }

        #endregion
    }
}

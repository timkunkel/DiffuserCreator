using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public static class Polygon2D
    {
        // Both polygons are shrunk slightly toward their centroids first, so faces that merely
        // touch along a shared fold edge do not count as overlapping.
        public static bool Overlaps(Vector2[] a, Vector2[] b, float insetFactor = 0.001f)
        {
            Vector2[] insetA = Inset(a, insetFactor);
            Vector2[] insetB = Inset(b, insetFactor);

            for (int i = 0; i < insetA.Length; i++)
            {
                Vector2 a1 = insetA[i];
                Vector2 a2 = insetA[(i + 1) % insetA.Length];

                for (int j = 0; j < insetB.Length; j++)
                {
                    if (SegmentsIntersect(a1, a2, insetB[j], insetB[(j + 1) % insetB.Length])) { return true; }
                }
            }

            return Contains(insetA, insetB[0]) || Contains(insetB, insetA[0]);
        }

        public static Vector2[] Inset(Vector2[] polygon, float factor)
        {
            Vector2 centroid = Centroid(polygon);
            var     result   = new Vector2[polygon.Length];

            for (int i = 0; i < polygon.Length; i++)
            {
                result[i] = centroid + (polygon[i] - centroid) * (1f - factor);
            }

            return result;
        }

        public static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];

                if (pi.y > point.y != pj.y > point.y
                    && point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static float Area(Vector2[] polygon)
        {
            float doubled = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                doubled += a.x * b.y - b.x * a.y;
            }

            return doubled * 0.5f;
        }

        public static Vector2 Centroid(Vector2[] polygon)
        {
            float   doubledArea = 0f;
            Vector2 sum         = Vector2.zero;

            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a     = polygon[i];
                Vector2 b     = polygon[(i + 1) % polygon.Length];
                float   cross = a.x * b.y - b.x * a.y;
                doubledArea += cross;
                sum         += (a + b) * cross;
            }

            if (Mathf.Abs(doubledArea) < 1e-9f)
            {
                foreach (Vector2 p in polygon) { sum += p; }
                return sum / polygon.Length;
            }

            return sum / (3f * doubledArea);
        }

        // Proper (crossing) intersection only; collinear touching is ignored, which is fine because
        // callers inset the polygons before testing.
        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d1 = Cross(p4 - p3, p1 - p3);
            float d2 = Cross(p4 - p3, p2 - p3);
            float d3 = Cross(p2 - p1, p3 - p1);
            float d4 = Cross(p2 - p1, p4 - p1);

            return (d1 > 0f && d2 < 0f || d1 < 0f && d2 > 0f)
                   && (d3 > 0f && d4 < 0f || d3 < 0f && d4 > 0f);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}

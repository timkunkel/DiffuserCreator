using UnityEngine;

namespace DiffuserCreator
{
    public static class GeometryUtils
    {
        // Intersection of two lines that are assumed coplanar. Returns false when the lines are
        // parallel or non-coplanar, so callers can avoid using a meaningless Vector3.zero result.
        public static bool LineLineIntersection(
            out Vector3 intersection,
            Vector3     linePoint1,
            Vector3     lineVec1,
            Vector3     linePoint2,
            Vector3     lineVec2)
        {
            Vector3 lineVec3      = linePoint2 - linePoint1;
            Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
            Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

            float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

            if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
            {
                float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
                intersection = linePoint1 + lineVec1 * s;
                return true;
            }

            intersection = Vector3.zero;
            return false;
        }
    }
}

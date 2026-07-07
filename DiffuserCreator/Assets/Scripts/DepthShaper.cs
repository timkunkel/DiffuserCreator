using UnityEngine;
using UnityEngine.ProBuilder;

namespace DiffuserCreator
{
    // Decides the four corner depths of a block. This is the logic that used to live inside
    // DiffuserBlock; pulling it out lets the grid pick a strategy at runtime and keeps the block
    // a pure geometry object.
    public abstract class DepthShaper
    {
        public abstract void Shape(DiffuserBlock block, DiffuserSettings settings);

        public static DepthShaper For(DepthSource source)
        {
            switch (source)
            {
                case DepthSource.Cutting: return new CuttingDepthShaper();
                case DepthSource.Curve:   return new CurveDepthShaper();
                case DepthSource.Manual:  return new ManualDepthShaper();
                default:                  return new ManualDepthShaper();
            }
        }
    }

    // Leaves the block's depths untouched (flat at its initial depth after generation).
    public sealed class ManualDepthShaper : DepthShaper
    {
        public override void Shape(DiffuserBlock block, DiffuserSettings settings)
        {
        }
    }

    // Carves each block by raycasting toward -Z against colliders on the cutting layer; the hit
    // distance becomes the depth. HeightMode decides how many rays and how corners share a depth.
    public sealed class CuttingDepthShaper : DepthShaper
    {
        public override void Shape(DiffuserBlock block, DiffuserSettings settings)
        {
            Transform transform = block.transform;
            var       depths    = new float[4];

            switch (settings.HeightMode)
            {
                case HeightMode.Middle:
                    float middle = Sample(block, settings, transform.position);
                    depths[0] = middle;
                    depths[1] = middle;
                    depths[2] = middle;
                    depths[3] = middle;
                    break;
                case HeightMode.Corner:
                    for (int i = 0; i < 4; i++)
                    {
                        depths[i] = Sample(block, settings, transform.position + DiffuserBlock.BackCorners[i]);
                    }

                    break;
                case HeightMode.Horizontal:
                    SampleEdge(block, settings, depths, 0, 1);
                    SampleEdge(block, settings, depths, 2, 3);
                    break;
                case HeightMode.Vertical:
                    SampleEdge(block, settings, depths, 0, 3);
                    SampleEdge(block, settings, depths, 1, 2);
                    break;
            }

            block.Angle = 0;
            block.SetDepths(depths[0], depths[1], depths[2], depths[3]);
        }

        private static void SampleEdge(DiffuserBlock block, DiffuserSettings settings, float[] depths, int a, int b)
        {
            Vector3 midpoint = DiffuserBlock.BackCorners[a]
                               + 0.5f * (DiffuserBlock.BackCorners[b] - DiffuserBlock.BackCorners[a]);
            float depth = Sample(block, settings, block.transform.position + midpoint);
            depths[a] = depth;
            depths[b] = depth;
        }

        private static float Sample(DiffuserBlock block, DiffuserSettings settings, Vector3 worldOrigin)
        {
            if (Physics.Raycast(worldOrigin, block.transform.TransformDirection(Vector3.back), out RaycastHit hit,
                                settings.DefaultDepth * block.Depth, settings.CuttingLayerMask))
            {
                return -block.transform.InverseTransformPoint(hit.point).z;
            }

            return settings.DefaultDepth;
        }
    }

    // Drives depth from AnimationCurves evaluated at the block's normalized grid position. In Height
    // mode the curve scales a flat depth; in Angle mode it tilts the front face by a snapped angle.
    public sealed class CurveDepthShaper : DepthShaper
    {
        public override void Shape(DiffuserBlock block, DiffuserSettings settings)
        {
            switch (settings.CurveMode)
            {
                case CurveMode.Height:
                    ShapeWithHeight(block, settings);
                    break;
                case CurveMode.Angle:
                    ShapeWithAngle(block, settings);
                    break;
            }
        }

        private static void ShapeWithHeight(DiffuserBlock block, DiffuserSettings settings)
        {
            Vector2 pos   = block.NormalizedPosition;
            float   value = 0f;
            int     count = 0;

            if (settings.UseHorizontalCurve)
            {
                value += settings.HorizontalCurve.Evaluate(pos.x);
                count++;
            }

            if (settings.UseVerticalCurve)
            {
                value += settings.VerticalCurve.Evaluate(pos.y);
                count++;
            }

            // The curve value is a fraction of the block's own depth (0..1), so a block never gets
            // deeper than its configured depth. With no curve enabled the block keeps full depth.
            float fraction = count > 0 ? Mathf.Clamp01(value / count) : 1f;

            fraction = value * 0.1f;

            block.Angle = 0;
            block.SetUniformDepth(block.InitialDepth * fraction);
        }

        private static void ShapeWithAngle(DiffuserBlock block, DiffuserSettings settings)
        {
            Vector2 pos        = block.NormalizedPosition;
            float   angleValue = 0f;
            int     curveCount = 0;

            if (settings.UseDiagonalCurve)
            {
                curveCount++;
                angleValue += settings.DiagonalCurve.Evaluate((pos.x + pos.y) / 2f);
            }

            if (settings.UseHorizontalCurve)
            {
                curveCount++;
                angleValue += settings.HorizontalCurve.Evaluate(pos.x);
            }

            if (settings.UseVerticalCurve)
            {
                curveCount++;
                angleValue += settings.VerticalCurve.Evaluate(pos.y);
            }

            if (curveCount > 0)
            {
                angleValue /= curveCount;
            }

            int snapAngle = Mathf.Max(1, settings.SnapAngle);
            int angle     = (int)Snapping.Snap(angleValue, snapAngle);
            block.Angle = angle;

            float   initialDepth = block.InitialDepth;
            Vector3 backTopRight = new Vector3(0.5f, 0.5f,  0f);
            Vector3 frontBottom  = new Vector3(0.5f, -0.5f, -initialDepth);
            Vector3 frontTop     = new Vector3(0.5f, 0.5f,  -initialDepth);

            Vector3 rightEdge  = frontTop - frontBottom;
            Vector3 tiltedEdge = Quaternion.Euler(angle, 0, 0) * rightEdge;

            if (GeometryUtils.LineLineIntersection(out Vector3 intersection, frontBottom, tiltedEdge,
                                                   backTopRight, frontTop - backTopRight))
            {
                float extraDepth = (frontTop - intersection).magnitude;
                float farDepth   = initialDepth + extraDepth;
                block.SetDepths(initialDepth, initialDepth, farDepth, farDepth);
            }
            else
            {
                block.SetUniformDepth(initialDepth);
            }
        }
    }
}

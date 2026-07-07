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

            // Sample the same three curves Angle mode does (diagonal at (x+y)/2), so enabling any
            // single curve toggle drives Height mode too. Each sample is normalized to that curve's
            // own min/max output, so curves authored for Angle mode (degree-scale values) still map
            // to a 0..1 height fraction instead of clamping flat.
            if (settings.UseDiagonalCurve)
            {
                value += NormalizedCurveValue(settings.DiagonalCurve, (pos.x + pos.y) / 2f);
                count++;
            }

            if (settings.UseHorizontalCurve)
            {
                value += NormalizedCurveValue(settings.HorizontalCurve, pos.x);
                count++;
            }

            if (settings.UseVerticalCurve)
            {
                value += NormalizedCurveValue(settings.VerticalCurve, pos.y);
                count++;
            }

            // Per-block height: the curves are sampled at THIS block's normalized grid position
            // (pos), so each block gets its own 0..1 fraction of the block depth and the wall varies
            // across the grid. With no curve enabled every block keeps full depth. Then
            // CurveHeightInfluence blends between full depth (0, curve ignored) and the curve
            // result (1), so a block never exceeds its configured depth.
            float curveFraction = count > 0 ? Mathf.Clamp01(value / count) : 1f;
            float influence     = Mathf.Clamp01(settings.CurveHeightInfluence);
            float fraction      = Mathf.Lerp(1f, curveFraction, influence);

            block.Angle = 0;
            block.SetUniformDepth(block.InitialDepth * fraction);
        }

        // Curve output at `time` mapped to 0..1 across the curve's own min/max key values. This lets
        // a curve authored in any range (e.g. degrees for Angle mode) still produce per-block height
        // variation; a flat curve (no range) contributes a constant, i.e. no variation.
        private static float NormalizedCurveValue(AnimationCurve curve, float time)
        {
            Keyframe[] keys = curve.keys;
            if (keys.Length == 0) { return 1f; }

            float min = keys[0].value;
            float max = keys[0].value;
            for (int i = 1; i < keys.Length; i++)
            {
                min = Mathf.Min(min, keys[i].value);
                max = Mathf.Max(max, keys[i].value);
            }

            float sample = curve.Evaluate(time);
            return Mathf.Approximately(max, min) ? Mathf.Clamp01(sample) : Mathf.InverseLerp(min, max, sample);
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

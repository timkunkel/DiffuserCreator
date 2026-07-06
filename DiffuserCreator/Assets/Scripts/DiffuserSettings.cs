using System;
using UnityEngine;

namespace DiffuserCreator
{
    public enum HeightMode
    {
        Middle,
        Corner,
        Horizontal,
        Vertical
    }

    public enum DepthSource
    {
        Cutting,
        Curve,
        Manual
    }

    public enum CurveMode
    {
        Height,
        Angle
    }

    // Immutable shaping parameters handed to a DepthShaper. The grid builds a fresh one each rebuild,
    // so shapers stay free of any reference back to the grid or its serialized fields.
    [Serializable]
    public class DiffuserSettings
    {
        public HeightMode HeightMode;
        public CurveMode  CurveMode;

        public bool           UseHorizontalCurve;
        public AnimationCurve HorizontalCurve;
        public bool           UseVerticalCurve;
        public AnimationCurve VerticalCurve;
        public bool           UseDiagonalCurve;
        public AnimationCurve DiagonalCurve;

        public int       SnapAngle;
        public LayerMask  CuttingLayerMask;
        public float      DefaultDepth;
    }
}

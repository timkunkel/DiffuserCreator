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

    // The single source of truth for a diffuser's configuration: grid layout, block size, and the
    // depth-shaping parameters. DiffuserGrid holds one of these as a [SerializeField] private field
    // and hands it to a DepthShaper (which only reads the shaping parameters and ignores the layout).
    [Serializable]
    public class DiffuserSettings
    {
        #region Grid

        [Min(1)] public int Rows    = 5;
        [Min(1)] public int Columns = 5;

        public float HorizontalSpacing = 0.2f;
        public float VerticalSpacing   = 0.2f;

        #endregion

        #region Block

        public float BlockWidth  = 1f;
        public float BlockHeight = 1f;
        public float BlockDepth  = 1f;

        #endregion

        #region Depth

        public DepthSource DepthSource = DepthSource.Curve;
        public HeightMode  HeightMode  = HeightMode.Middle;
        public LayerMask   CuttingLayerMask;
        public float       DefaultDepth = 1f;

        #endregion

        #region Curve

        public CurveMode CurveMode = CurveMode.Height;

        public bool           UseHorizontalCurve;
        public AnimationCurve HorizontalCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool           UseVerticalCurve;
        public AnimationCurve VerticalCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool           UseDiagonalCurve;
        public AnimationCurve DiagonalCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Min(1)] public int SnapAngle = 5;

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public enum LineKind
    {
        Cut,
        Fold,
        CropMark
    }

    public class PapercraftPolyline
    {
        public Vector2[] Points;
        public LineKind  Kind;
    }

    public class PapercraftLabel
    {
        public Vector2 Position;
        public string  Text;
        public float   HeightMm;
    }

    // One printable page in millimeters, origin at the top-left corner, y growing downward.
    public class PapercraftPage
    {
        public Vector2 SizeMm;

        public readonly List<PapercraftPolyline> Polylines = new List<PapercraftPolyline>();
        public readonly List<PapercraftLabel>    Labels    = new List<PapercraftLabel>();
    }
}

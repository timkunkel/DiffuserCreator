using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // All knobs for the papercraft export in one place. Paper distances are millimeters;
    // MillimetersPerModelUnit is the print scale (100 means 1 model unit becomes 100 mm on paper,
    // i.e. 1:10 for meter-based models).
    public class PapercraftOptions
    {
        public static readonly Vector2 PAGE_A4_MM     = new Vector2(210f, 297f);
        public static readonly Vector2 PAGE_LETTER_MM = new Vector2(215.9f, 279.4f);

        public float   MillimetersPerModelUnit = 100f;
        public Vector2 PageSizeMm              = PAGE_A4_MM;
        public float   PageMarginMm            = 10f;
        public float   PieceSpacingMm          = 6f;
        public float   TabHeightMm             = 8f;
        public float   TabShoulderAngleDeg     = 60f;
        public float   LabelHeightMm           = 3f;
        public bool    AddCropMarks            = true;
        public float   WeldTolerance           = 1e-4f;
        public float   CoplanarToleranceDeg    = 0.5f;

        public static float InchesToMillimeters(float inches)
        {
            return inches * 25.4f;
        }
    }
}

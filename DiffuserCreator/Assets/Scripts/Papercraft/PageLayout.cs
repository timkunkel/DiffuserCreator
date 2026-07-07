using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    // Shelf-packs piece drawings onto fixed-size pages, tallest pieces first, and converts the
    // piece-local y-up coordinates into page space (y down, origin at the top-left corner).
    public static class PageLayout
    {
        public static List<PapercraftPage> Pack(IReadOnlyList<PieceDrawing> drawings, PapercraftOptions options)
        {
            var pages = new List<PapercraftPage>();

            float innerWidth  = options.PageSizeMm.x - 2f * options.PageMarginMm;
            float innerHeight = options.PageSizeMm.y - 2f * options.PageMarginMm;

            PapercraftPage page        = null;
            float          cursorX     = 0f;
            float          cursorY     = 0f;
            float          shelfHeight = 0f;

            foreach (PieceDrawing drawing in drawings.OrderByDescending(d => d.Bounds.height))
            {
                float width  = drawing.Bounds.width;
                float height = drawing.Bounds.height;

                if (width > innerWidth || height > innerHeight)
                {
                    Debug.LogWarning($"Papercraft: piece ({width:0.#} x {height:0.#} mm) is larger than the printable page area, reduce MillimetersPerModelUnit.");
                }

                if (page != null && cursorX > 0f && cursorX + width > innerWidth)
                {
                    cursorY    += shelfHeight + options.PieceSpacingMm;
                    cursorX     = 0f;
                    shelfHeight = 0f;
                }

                if (page == null || cursorY > 0f && cursorY + height > innerHeight)
                {
                    page = NewPage(options);
                    pages.Add(page);
                    cursorX     = 0f;
                    cursorY     = 0f;
                    shelfHeight = 0f;
                }

                PlaceDrawing(page, drawing, options.PageMarginMm + cursorX, options.PageMarginMm + cursorY);

                cursorX     += width + options.PieceSpacingMm;
                shelfHeight  = Mathf.Max(shelfHeight, height);
            }

            return pages;
        }

        private static void PlaceDrawing(PapercraftPage page, PieceDrawing drawing, float offsetX, float offsetY)
        {
            foreach (PapercraftPolyline polyline in drawing.Polylines)
            {
                var points = new Vector2[polyline.Points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = ToPage(polyline.Points[i], drawing.Bounds, offsetX, offsetY);
                }

                page.Polylines.Add(new PapercraftPolyline { Points = points, Kind = polyline.Kind });
            }

            foreach (PapercraftLabel label in drawing.Labels)
            {
                page.Labels.Add(new PapercraftLabel
                {
                    Position = ToPage(label.Position, drawing.Bounds, offsetX, offsetY),
                    Text     = label.Text,
                    HeightMm = label.HeightMm
                });
            }
        }

        // Flipping y here is a change of viewing convention, not a mirror: the printed side stays
        // the model's outside surface.
        private static Vector2 ToPage(Vector2 point, Rect bounds, float offsetX, float offsetY)
        {
            return new Vector2(offsetX + point.x - bounds.xMin, offsetY + bounds.yMax - point.y);
        }

        private static PapercraftPage NewPage(PapercraftOptions options)
        {
            var page = new PapercraftPage { SizeMm = options.PageSizeMm };
            if (options.AddCropMarks)
            {
                AddCropMarks(page, options);
            }

            return page;
        }

        private static void AddCropMarks(PapercraftPage page, PapercraftOptions options)
        {
            float margin = options.PageMarginMm;
            float far    = Mathf.Min(7f, margin - 1f);
            float near   = Mathf.Min(2f, margin - 1f);
            if (far <= near) { return; }

            var corners = new[]
            {
                (new Vector2(margin, margin), new Vector2(-1f, -1f)),
                (new Vector2(page.SizeMm.x - margin, margin), new Vector2(1f, -1f)),
                (new Vector2(margin, page.SizeMm.y - margin), new Vector2(-1f, 1f)),
                (new Vector2(page.SizeMm.x - margin, page.SizeMm.y - margin), new Vector2(1f, 1f))
            };

            foreach ((Vector2 corner, Vector2 sign) in corners)
            {
                page.Polylines.Add(new PapercraftPolyline
                {
                    Kind   = LineKind.CropMark,
                    Points = new[] { corner + new Vector2(sign.x * far, 0f), corner + new Vector2(sign.x * near, 0f) }
                });
                page.Polylines.Add(new PapercraftPolyline
                {
                    Kind   = LineKind.CropMark,
                    Points = new[] { corner + new Vector2(0f, sign.y * far), corner + new Vector2(0f, sign.y * near) }
                });
            }
        }
    }
}

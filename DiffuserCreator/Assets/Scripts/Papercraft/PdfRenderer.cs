using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DiffuserCreator.Papercraft
{
    // Minimal self-contained PDF 1.4 writer. Vector polylines plus the built-in Helvetica font
    // need no font embedding and no external library, which keeps the export runtime-safe inside
    // Unity (the vendored PDFsharp stays unwired). Output is pure ASCII, so byte offsets in the
    // cross-reference table equal StringBuilder character offsets.
    public static class PdfRenderer
    {
        private const float POINTS_PER_MM      = 72f / 25.4f;
        private const float HELVETICA_DIGIT_EM = 0.556f;

        public static byte[] Render(IReadOnlyList<PapercraftPage> pages)
        {
            int objectCount = 3 + pages.Count * 2;
            var objects     = new string[objectCount + 1];

            var kids = new StringBuilder();
            for (int i = 0; i < pages.Count; i++)
            {
                kids.Append(4 + i * 2).Append(" 0 R ");
            }

            objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
            objects[2] = $"<< /Type /Pages /Kids [{kids.ToString().TrimEnd()}] /Count {pages.Count} >>";
            objects[3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

            for (int i = 0; i < pages.Count; i++)
            {
                PapercraftPage page       = pages[i];
                int            pageObject = 4 + i * 2;
                string         content    = BuildContentStream(page);

                objects[pageObject] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "
                                      + F(page.SizeMm.x * POINTS_PER_MM) + " " + F(page.SizeMm.y * POINTS_PER_MM)
                                      + "] /Resources << /Font << /F1 3 0 R >> >> /Contents " + (pageObject + 1) + " 0 R >>";
                objects[pageObject + 1] = $"<< /Length {content.Length} >>\nstream\n{content}\nendstream";
            }

            return Assemble(objects);
        }

        #region Content stream

        private static string BuildContentStream(PapercraftPage page)
        {
            var sb = new StringBuilder();

            AppendStrokes(sb, page, LineKind.Cut, "[] 0 d\n0.71 w\n0 G\n");
            AppendStrokes(sb, page, LineKind.Fold, "[8.5 4.25] 0 d\n0.57 w\n0.4 G\n");
            AppendStrokes(sb, page, LineKind.CropMark, "[] 0 d\n0.43 w\n0.6 G\n");

            foreach (PapercraftLabel label in page.Labels)
            {
                AppendLabel(sb, page, label);
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static void AppendStrokes(StringBuilder sb, PapercraftPage page, LineKind kind, string setup)
        {
            bool any = false;
            foreach (PapercraftPolyline polyline in page.Polylines)
            {
                if (polyline.Kind != kind) { continue; }

                if (!any)
                {
                    sb.Append(setup);
                    any = true;
                }

                sb.Append(X(polyline.Points[0].x)).Append(' ').Append(Y(page, polyline.Points[0].y)).Append(" m\n");
                for (int i = 1; i < polyline.Points.Length; i++)
                {
                    sb.Append(X(polyline.Points[i].x)).Append(' ').Append(Y(page, polyline.Points[i].y)).Append(" l\n");
                }
                sb.Append("S\n");
            }
        }

        private static void AppendLabel(StringBuilder sb, PapercraftPage page, PapercraftLabel label)
        {
            float size = label.HeightMm * POINTS_PER_MM;
            float x    = label.Position.x * POINTS_PER_MM - HELVETICA_DIGIT_EM * size * label.Text.Length * 0.5f;
            float y    = (page.SizeMm.y - label.Position.y) * POINTS_PER_MM - size * 0.35f;

            sb.Append("BT\n/F1 ").Append(F(size)).Append(" Tf\n")
              .Append(F(x)).Append(' ').Append(F(y)).Append(" Td\n(")
              .Append(Escape(label.Text)).Append(") Tj\nET\n");
        }

        #endregion

        #region Document assembly

        private static byte[] Assemble(string[] objects)
        {
            var sb      = new StringBuilder("%PDF-1.4\n");
            var offsets = new int[objects.Length];

            for (int n = 1; n < objects.Length; n++)
            {
                offsets[n] = sb.Length;
                sb.Append(n).Append(" 0 obj\n").Append(objects[n]).Append("\nendobj\n");
            }

            int xrefStart = sb.Length;
            sb.Append("xref\n0 ").Append(objects.Length).Append('\n');
            sb.Append("0000000000 65535 f \n");
            for (int n = 1; n < objects.Length; n++)
            {
                sb.Append(offsets[n].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }

            sb.Append("trailer\n<< /Size ").Append(objects.Length).Append(" /Root 1 0 R >>\n")
              .Append("startxref\n").Append(xrefStart).Append("\n%%EOF");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        #endregion

        #region Helpers

        private static string Escape(string text)
        {
            return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static string X(float millimeters)
        {
            return F(millimeters * POINTS_PER_MM);
        }

        private static string Y(PapercraftPage page, float millimeters)
        {
            return F((page.SizeMm.y - millimeters) * POINTS_PER_MM);
        }

        private static string F(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}

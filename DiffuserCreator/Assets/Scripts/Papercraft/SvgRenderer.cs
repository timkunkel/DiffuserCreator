using System.Globalization;
using System.Text;

namespace DiffuserCreator.Papercraft
{
    // Renders one page to a standalone SVG string. Coordinates are millimeters, matching the
    // viewBox 1:1, so browsers print the net at true scale (print dialogs must be set to 100%).
    public static class SvgRenderer
    {
        public static string Render(PapercraftPage page)
        {
            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(page.SizeMm.x))
              .Append("mm\" height=\"").Append(F(page.SizeMm.y))
              .Append("mm\" viewBox=\"0 0 ").Append(F(page.SizeMm.x)).Append(' ').Append(F(page.SizeMm.y))
              .AppendLine("\">");

            sb.AppendLine("  <g fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
            foreach (PapercraftPolyline polyline in page.Polylines)
            {
                AppendPath(sb, polyline);
            }
            sb.AppendLine("  </g>");

            foreach (PapercraftLabel label in page.Labels)
            {
                AppendLabel(sb, label);
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void AppendPath(StringBuilder sb, PapercraftPolyline polyline)
        {
            sb.Append("    <path d=\"M ").Append(F(polyline.Points[0].x)).Append(' ').Append(F(polyline.Points[0].y));
            for (int i = 1; i < polyline.Points.Length; i++)
            {
                sb.Append(" L ").Append(F(polyline.Points[i].x)).Append(' ').Append(F(polyline.Points[i].y));
            }
            sb.Append("\" ").Append(StyleFor(polyline.Kind)).AppendLine("/>");
        }

        private static void AppendLabel(StringBuilder sb, PapercraftLabel label)
        {
            sb.Append("  <text x=\"").Append(F(label.Position.x))
              .Append("\" y=\"").Append(F(label.Position.y))
              .Append("\" font-family=\"Helvetica, Arial, sans-serif\" font-size=\"").Append(F(label.HeightMm))
              .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"#000000\">")
              .Append(Escape(label.Text))
              .AppendLine("</text>");
        }

        private static string StyleFor(LineKind kind)
        {
            switch (kind)
            {
                case LineKind.Fold:     return "stroke=\"#666666\" stroke-width=\"0.2\" stroke-dasharray=\"3 1.5\"";
                case LineKind.CropMark: return "stroke=\"#999999\" stroke-width=\"0.15\"";
                default:                return "stroke=\"#000000\" stroke-width=\"0.25\"";
            }
        }

        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

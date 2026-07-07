using System.IO;

namespace DiffuserCreator.Papercraft
{
    public static class PapercraftFiles
    {
        // Writes the multi-page PDF to pdfPath and one SVG per page beside it (basename_page01.svg,
        // ...). Pure System.IO, so it is safe both at runtime and in the editor.
        public static void Write(PapercraftResult result, string pdfPath)
        {
            File.WriteAllBytes(pdfPath, result.PdfBytes);

            string directory = Path.GetDirectoryName(pdfPath) ?? string.Empty;
            string baseName  = Path.GetFileNameWithoutExtension(pdfPath);
            for (int i = 0; i < result.SvgPages.Length; i++)
            {
                File.WriteAllText(Path.Combine(directory, $"{baseName}_page{i + 1:00}.svg"), result.SvgPages[i]);
            }
        }
    }
}

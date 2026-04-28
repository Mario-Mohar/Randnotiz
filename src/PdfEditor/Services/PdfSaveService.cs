using Randnotiz.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace Randnotiz.Services;

public class PdfSaveService
{
    public void Save(string inputPath, string outputPath, IReadOnlyList<PdfPageModel> pages)
    {
        using var document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);

        foreach (var pageModel in pages)
        {
            if (pageModel.PageIndex >= document.PageCount) continue;
            var pdfPage = document.Pages[pageModel.PageIndex];

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            foreach (var annotation in pageModel.Annotations)
            {
                if (string.IsNullOrEmpty(annotation.Text)) continue;

                // Convert from display coordinates to PDF points
                double scaleX = pdfPage.Width.Point / pageModel.WidthInPoints;
                double scaleY = pdfPage.Height.Point / pageModel.HeightInPoints;

                var font = new XFont(annotation.FontFamily, annotation.FontSize * scaleY);

                double pdfX = annotation.X * scaleX;
                double pdfY = annotation.Y * scaleY;

                // Top-left alignment to match WPF Canvas positioning
                var format = new XStringFormat
                {
                    Alignment = XStringAlignment.Near,
                    LineAlignment = XLineAlignment.Near
                };
                gfx.DrawString(annotation.Text, font, XBrushes.Black, pdfX, pdfY, format);
            }
        }

        document.Save(outputPath);
    }
}

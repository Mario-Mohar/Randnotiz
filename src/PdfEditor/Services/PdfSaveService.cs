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

            // Die Annotationen liegen im angezeigten, also schon gedrehten
            // Rahmen. XGraphics zeichnet dagegen in den ungedrehten Rahmen der
            // Seite (links oben, y nach unten). /Rotate dreht im Uhrzeigersinn.
            int rotate = ((pdfPage.Elements.GetInteger("/Rotate") % 360) + 360) % 360;
            double w = pdfPage.MediaBox.Width;
            double h = pdfPage.MediaBox.Height;

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            foreach (var annotation in pageModel.Annotations)
            {
                if (string.IsNullOrEmpty(annotation.Text)) continue;

                // Position und Groesse sind schon PDF-Punkte, unabhaengig vom Zoom.
                var font = new XFont(annotation.FontFamily, annotation.FontSize);

                double x = annotation.X;
                double y = annotation.Y;
                var (pdfX, pdfY) = rotate switch
                {
                    90 => (y, h - x),
                    180 => (w - x, h - y),
                    270 => (w - y, x),
                    _ => (x, y)
                };

                // Top-left alignment to match WPF Canvas positioning
                var format = new XStringFormat
                {
                    Alignment = XStringAlignment.Near,
                    LineAlignment = XLineAlignment.Near
                };

                // Gegen die Seitendrehung zurueckdrehen, damit der Text in der
                // Anzeige waagrecht steht.
                var state = gfx.Save();
                gfx.TranslateTransform(pdfX, pdfY);
                if (rotate != 0) gfx.RotateTransform(-rotate);
                gfx.DrawString(annotation.Text, font, XBrushes.Black, 0, 0, format);
                gfx.Restore(state);
            }
        }

        document.Save(outputPath);
    }
}

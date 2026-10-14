using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Randnotiz.Models;
using Randnotiz.Services;

namespace Randnotiz.Tests.Services;

/// <summary>
/// Speichert Annotationen und rendert das Ergebnis wieder durch PDFium. So
/// wird geprueft, wo der Text auf der angezeigten Seite wirklich landet, und
/// nicht nur, dass irgendetwas geschrieben wurde.
/// </summary>
[Collection(PdfiumCollection.Name)]
public class AnnotationPlacementTests
{
    public AnnotationPlacementTests()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new CrossPlatformFontResolver();
    }

    internal static string CreateBlankPdf(params (double Width, double Height, int Rotate)[] pages)
    {
        var dir = Path.Combine(Path.GetTempPath(), "randnotiz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "leer.pdf");
        using var doc = new PdfDocument();
        foreach (var (w, h, rotate) in pages)
        {
            var page = doc.AddPage();
            page.Width = XUnitPt.FromPoint(w);
            page.Height = XUnitPt.FromPoint(h);
            // Wie qpdf --rotate: nur der Eintrag, der Inhalt bleibt.
            if (rotate != 0) page.Elements.SetInteger("/Rotate", rotate);
        }
        doc.Save(path);
        return path;
    }

    internal static void Delete(string path) => Directory.Delete(Path.GetDirectoryName(path)!, true);

    /// <summary>Umriss alles Gezeichneten auf der angezeigten Seite, in
    /// PDF-Punkten von links oben. Null, wenn die Seite leer ist.</summary>
    internal static async Task<(double Left, double Top, double Right, double Bottom)?> InkBounds(
        string path, int pageIndex = 0)
    {
        var service = new PdfRenderService();
        await service.LoadAsync(path);
        var page = await service.RenderPagePixelsAsync(pageIndex, 72);
        var (widthPt, _) = service.GetPageSize(pageIndex);
        double scale = page.Width / widthPt;

        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < page.Height; y++)
        for (int x = 0; x < page.Width; x++)
        {
            if (page.Pixels[(y * page.Width + x) * 4 + 3] < 128) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        if (right < 0) return null;
        return (left / scale, top / scale, (right + 1) / scale, (bottom + 1) / scale);
    }

    private static List<PdfPageModel> OnePage(double width, double height, TextAnnotation annotation)
    {
        var pages = new List<PdfPageModel> { new(0) { WidthInPoints = width, HeightInPoints = height } };
        pages[0].Annotations.Add(annotation);
        return pages;
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public async Task Schriftgroesse_IstImPdfInPunkten(double fontSize)
    {
        // "HH" hat keine Unterlaengen: die Hoehe der Tinte ist die
        // Versalhoehe, bei Arial und Liberation Sans rund 0,72 der Groesse.
        var input = CreateBlankPdf((595, 842, 0));
        var output = Path.Combine(Path.GetDirectoryName(input)!, "aus.pdf");
        try
        {
            var annotation = new TextAnnotation(100, 200, 0) { Text = "HH", FontSize = fontSize };
            new PdfSaveService().Save(input, output, OnePage(595, 842, annotation));

            var ink = await InkBounds(output);
            Assert.NotNull(ink);
            var (left, top, _, bottom) = ink.Value;
            Assert.InRange(bottom - top, fontSize * 0.65, fontSize * 0.8);
            Assert.InRange(left, 100, 100 + fontSize * 0.3);
            Assert.InRange(top, 200, 200 + fontSize * 0.5);
        }
        finally
        {
            Delete(input);
        }
    }

    [Theory]
    [InlineData(595, 842, 0)]
    [InlineData(595, 842, 90)]
    [InlineData(595, 842, 180)]
    [InlineData(595, 842, 270)]
    [InlineData(842, 595, 90)]
    [InlineData(842, 595, -90)]
    public async Task GedrehteSeite_TextStehtWoErAngezeigtWurdeUndWaagrecht(
        double mediaWidth, double mediaHeight, int rotate)
    {
        // Wie qpdf --rotate=+90: nur /Rotate gesetzt. Angezeigt wird dann
        // gedreht, und die Annotation liegt in diesem angezeigten Rahmen.
        var input = CreateBlankPdf((mediaWidth, mediaHeight, rotate));
        var output = Path.Combine(Path.GetDirectoryName(input)!, "aus.pdf");
        try
        {
            bool quer = Math.Abs(rotate) % 180 == 90;
            double width = quer ? mediaHeight : mediaWidth, height = quer ? mediaWidth : mediaHeight;
            var annotation = new TextAnnotation(50, 60, 0) { Text = "HHHH", FontSize = 20 };
            new PdfSaveService().Save(input, output, OnePage(width, height, annotation));

            var ink = await InkBounds(output);
            Assert.NotNull(ink);
            var (left, top, right, bottom) = ink.Value;
            Assert.InRange(left, 50, 56);
            Assert.InRange(top, 60, 70);
            // Waagrecht: breiter als hoch, und so hoch wie die Versalhoehe.
            Assert.True(right - left > 2 * (bottom - top), $"Text steht quer: {ink}");
            Assert.InRange(bottom - top, 20 * 0.65, 20 * 0.8);
        }
        finally
        {
            Delete(input);
        }
    }
}

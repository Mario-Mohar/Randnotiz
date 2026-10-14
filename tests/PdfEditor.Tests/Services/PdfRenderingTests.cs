using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Randnotiz.Services;

namespace Randnotiz.Tests.Services;

/// <summary>
/// Rendert echte, mit PDFsharp erzeugte Seiten durch PDFium. Die erwarteten
/// Pixelmasse sind am 2026-10-11 mit dem alten Docnet.Core 2.6.0 gemessen
/// worden; sie halten fest, dass der Wechsel auf die eigene PDFium-Anbindung
/// Groesse, Massstab und Drehung nicht veraendert.
/// </summary>
[Collection(PdfiumCollection.Name)]
public class PdfRenderingTests
{
    // Jede Seite bekommt oben links ein rotes Rechteck ueber die halbe Breite
    // und ein Viertel der Hoehe, der Rest bleibt leer.
    private static string CreatePdf(string? fileName = null,
                                    params (double Width, double Height, int Rotate)[] pages)
    {
        var dir = Path.Combine(Path.GetTempPath(), "randnotiz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName ?? "seite.pdf");
        using var doc = new PdfDocument();
        foreach (var (w, h, rotate) in pages)
        {
            var page = doc.AddPage();
            page.Width = XUnitPt.FromPoint(w);
            page.Height = XUnitPt.FromPoint(h);
            page.Rotate = rotate;
            using var g = XGraphics.FromPdfPage(page);
            g.DrawRectangle(XBrushes.Red, 0, 0, w / 2, h / 4);
        }
        doc.Save(path);
        return path;
    }

    private static void Delete(string path) => Directory.Delete(Path.GetDirectoryName(path)!, true);

    private static (byte B, byte G, byte R, byte A) Pixel(PdfRenderService.RenderedPage page, int x, int y)
    {
        int i = (y * page.Width + x) * 4;
        return (page.Pixels[i], page.Pixels[i + 1], page.Pixels[i + 2], page.Pixels[i + 3]);
    }

    [Theory]
    // A4 hoch bei 72, 96, 150 dpi und 125 % Zoom (120 dpi)
    [InlineData(595, 842, 0, 72, 712, 1008)]
    [InlineData(595, 842, 0, 96, 949, 1344)]
    [InlineData(595, 842, 0, 150, 1483, 2100)]
    [InlineData(595, 842, 0, 120, 1187, 1680)]
    // A4 quer, A4 hoch mit /Rotate 90, US Legal, A2
    [InlineData(842, 595, 0, 96, 1344, 949)]
    [InlineData(595, 842, 90, 96, 1344, 949)]
    [InlineData(612, 1008, 0, 96, 816, 1344)]
    [InlineData(1190, 1684, 0, 96, 949, 1344)]
    // Krumme Masse mit /Rotate 270: hier wird abgeschnitten, nicht gerundet
    [InlineData(300.5, 200.25, 270, 96, 895, 1343)]
    [InlineData(300.5, 200.25, 270, 150, 1399, 2100)]
    public async Task Seite_HatDieselbenPixelmasseWieMitDocnet(
        double width, double height, int rotate, double dpi, int expectedWidth, int expectedHeight)
    {
        var path = CreatePdf(null, (width, height, rotate));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            var page = await service.RenderPagePixelsAsync(0, dpi);

            Assert.Equal(expectedWidth, page.Width);
            Assert.Equal(expectedHeight, page.Height);
            Assert.Equal(expectedWidth * expectedHeight * 4, page.Pixels.Length);
        }
        finally
        {
            Delete(path);
        }
    }

    [Theory]
    [InlineData(595, 842, 0, 595, 842)]
    [InlineData(595, 842, 90, 842, 595)]
    [InlineData(595, 842, 180, 595, 842)]
    [InlineData(300.5, 200.25, 270, 200.25, 300.5)]
    public async Task Seitengroesse_InPunktenMitDrehung(
        double width, double height, int rotate, double expectedWidth, double expectedHeight)
    {
        var path = CreatePdf(null, (width, height, rotate));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            var (w, h) = service.GetPageSize(0);
            Assert.Equal(expectedWidth, w, 0.01);
            Assert.Equal(expectedHeight, h, 0.01);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task Seite_IstNichtLeerUndRichtigHerum()
    {
        var path = CreatePdf(null, (595, 842, 0));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            var page = await service.RenderPagePixelsAsync(0, 96);

            // Oben links liegt das rote Rechteck, BGRA also 00 00 ff ff.
            Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(page, 2, 2));
            // Unten rechts nichts: der Hintergrund bleibt durchsichtig wie bisher.
            Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), Pixel(page, page.Width - 2, page.Height - 2));

            // Und ungefaehr ein Achtel der Flaeche ist gefuellt.
            int filled = 0;
            for (int i = 3; i < page.Pixels.Length; i += 4)
                if (page.Pixels[i] != 0) filled++;
            double share = filled / (double)(page.Width * page.Height);
            Assert.InRange(share, 0.12, 0.13);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task GedrehteSeite_LiegtDasRechteckAnDerGedrehtenStelle()
    {
        // /Rotate 90 dreht im Uhrzeigersinn: aus oben links wird oben rechts.
        var path = CreatePdf(null, (595, 842, 90));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            var page = await service.RenderPagePixelsAsync(0, 96);

            Assert.Equal(255, Pixel(page, page.Width - 2, 2).A);
            Assert.Equal(0, Pixel(page, 2, 2).A);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task PfadMitUmlauten_WirdGeoeffnet()
    {
        // PDFium erwartet den Pfad als UTF-8, auf jedem System.
        var path = CreatePdf("Rändernotiz äöü ß.pdf", (595, 842, 0), (595, 842, 0));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            Assert.Equal(2, service.PageCount);
            var page = await service.RenderPagePixelsAsync(1, 72);
            Assert.Equal(712, page.Width);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task KeinePdf_WirftVerstaendlicheMeldung()
    {
        var dir = Path.Combine(Path.GetTempPath(), "randnotiz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "kaputt.pdf");
        File.WriteAllText(path, "das ist keine PDF");
        try
        {
            var service = new PdfRenderService();
            var ex = await Assert.ThrowsAsync<PdfiumException>(() => service.LoadAsync(path));
            Assert.Contains("keine gültige PDF", ex.Message);

            var missing = await Assert.ThrowsAsync<PdfiumException>(
                () => service.LoadAsync(Path.Combine(dir, "gibtesnicht.pdf")));
            Assert.Contains("nicht gefunden", missing.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SeiteAusserhalbDesDokuments_Wirft()
    {
        var path = CreatePdf(null, (595, 842, 0));
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            await Assert.ThrowsAsync<PdfiumException>(() => service.RenderPagePixelsAsync(1, 72));
            // Und die Sperre ist danach wieder frei.
            var page = await service.RenderPagePixelsAsync(0, 72);
            Assert.Equal(712, page.Width);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task GleichzeitigesRendern_LiefertUeberallDasselbe()
    {
        var path = CreatePdf(null, (595, 842, 0), (842, 595, 0), (595, 842, 90));
        try
        {
            var services = Enumerable.Range(0, 4).Select(_ => new PdfRenderService()).ToList();
            foreach (var s in services) await s.LoadAsync(path);

            var tasks = services.SelectMany(s => Enumerable.Range(0, 3)
                .Select(i => s.RenderPagePixelsAsync(i, 72))).ToList();
            var pages = await Task.WhenAll(tasks);

            for (int i = 0; i < pages.Length; i++)
            {
                var reference = pages[i % 3];
                Assert.Equal(reference.Width, pages[i].Width);
                Assert.Equal(reference.Pixels, pages[i].Pixels);
            }
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task RenderPageAsync_LiefertBitmapMitMassenUndDpi()
    {
        // Der ganze Weg bis zur Avalonia-Bitmap. WriteableBitmap braucht eine
        // Plattform und deren UI-Thread; beides liefert die Headless-Sitzung.
        var path = CreatePdf(null, (595, 842, 0));
        try
        {
            var (size, dpi, opaque) = await HeadlessTestApp.Session.Dispatch(async () =>
            {
                var service = new PdfRenderService();
                await service.LoadAsync(path);
                using var bitmap = await service.RenderPageAsync(0, 96);
                // Ein Pixel oben links zurueckholen: dort liegt das Rechteck.
                var pixel = new byte[4];
                unsafe
                {
                    fixed (byte* p = pixel)
                        bitmap.CopyPixels(new PixelRect(2, 2, 1, 1), (IntPtr)p, 4, 4);
                }
                return (bitmap.PixelSize, bitmap.Dpi, pixel[3]);
            }, CancellationToken.None);

            Assert.Equal(new PixelSize(949, 1344), size);
            Assert.Equal(new Vector(96, 96), dpi);
            Assert.Equal(255, opaque);
        }
        finally
        {
            Delete(path);
        }
    }
}

/// <summary>
/// Alle Tests, die PDFium anfassen, laufen nacheinander. Die RenderLock ist
/// prozessweit, und PdfRenderServiceTests prueft ihren Zaehlerstand direkt;
/// liefe daneben ein Render aus einer anderen Klasse, saehe der Test eine
/// belegte Sperre, die gar nicht seine ist.
/// </summary>
[CollectionDefinition(Name)]
public class PdfiumCollection
{
    public const string Name = "PDFium";
}

/// <summary>Eine Headless-Sitzung fuer alle Tests, die eine Avalonia-Plattform
/// brauchen. Sie lebt bis zum Ende des Testlaufs.</summary>
public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Avalonia.Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static readonly Lazy<HeadlessUnitTestSession> LazySession =
        new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp)));

    public static HeadlessUnitTestSession Session => LazySession.Value;
}

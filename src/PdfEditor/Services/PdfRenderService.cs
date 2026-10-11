using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Randnotiz.Services;

public class PdfRenderService
{
    private string? _filePath;
    private int _pageCount;

    // Ein Platz, weil pdfium hier ohnehin nicht parallelisiert wird: es geht um
    // Sicherheit, nicht um Durchsatz. Statisch, also prozessweit -- das ist
    // richtig, weil PDFium genau einmal pro Prozess initialisiert wird und
    // selbst nicht threadsicher ist. Jeder Aufruf in Pdfium laeuft hierunter.
    private static readonly SemaphoreSlim RenderLock = new(1, 1);

    public int PageCount => _pageCount;

    public async Task LoadAsync(string filePath)
    {
        _filePath = filePath;
        // Dieselbe Sperre wie beim Rendern: das Oeffnen greift auch von hier
        // auf dieselbe native Bibliothek zu, und ein Dokumentwechsel kann auf
        // einen noch laufenden Render des vorigen treffen.
        await RenderLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Die Bibliothek selbst bleibt initialisiert, das Dokument
            // darunter wird dagegen sehr wohl pro Aufruf wieder geschlossen.
            var document = Pdfium.OpenDocument(filePath);
            try
            {
                _pageCount = Pdfium.FPDF_GetPageCount(document);
            }
            finally
            {
                Pdfium.FPDF_CloseDocument(document);
            }
        }
        finally
        {
            RenderLock.Release();
        }
    }

    /// <summary>
    /// Rendert eine Seite. Der Token bestellt einen Auftrag ab, der noch nicht
    /// begonnen hat oder noch in der Warteschlange steht.
    /// </summary>
    /// <remarks>
    /// Mitten im nativen Aufruf laesst sich nichts unterbrechen: Oeffnen und
    /// Rendern laufen durch. Die Koernung ist damit eine Seite, und das
    /// reicht -- eine Seite ist schnell durch, waehrend zwanzig aufgereihte
    /// Seiten genau das sind, worum es geht.
    /// </remarks>
    public Task<Bitmap> RenderPageAsync(int pageIndex, double dpi = 150,
                                        CancellationToken cancellationToken = default)
    {
        if (_filePath is null) throw new InvalidOperationException("No PDF loaded.");
        var filePath = _filePath; // capture before entering background thread

        return Task.Run(async () =>
        {
            var page = await RenderPixelsLockedAsync(filePath, pageIndex, dpi, cancellationToken)
                .ConfigureAwait(false);

            var bmp = new WriteableBitmap(
                new PixelSize(page.Width, page.Height),
                new Vector(dpi, dpi),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using var fb = bmp.Lock();
            int sourceStride = page.Width * Pdfium.BytesPerPixel;
            if (fb.RowBytes == sourceStride)
            {
                Marshal.Copy(page.Pixels, 0, fb.Address, page.Pixels.Length);
            }
            else
            {
                for (int row = 0; row < page.Height; row++)
                    Marshal.Copy(page.Pixels, row * sourceStride, fb.Address + row * fb.RowBytes, sourceStride);
            }

            return (Bitmap)bmp;
        }, cancellationToken);
    }

    /// <summary>Eine gerenderte Seite als BGRA ohne Zeilenauffuellung, also
    /// genau <c>Width * 4</c> Byte je Zeile.</summary>
    internal readonly record struct RenderedPage(int Width, int Height, byte[] Pixels);

    /// <summary>
    /// Der native Teil von <see cref="RenderPageAsync"/>, ohne Avalonia: so
    /// laesst er sich ohne Renderplattform testen.
    /// </summary>
    internal Task<RenderedPage> RenderPagePixelsAsync(int pageIndex, double dpi = 150,
                                                      CancellationToken cancellationToken = default)
    {
        if (_filePath is null) throw new InvalidOperationException("No PDF loaded.");
        var filePath = _filePath;
        return Task.Run(() => RenderPixelsLockedAsync(filePath, pageIndex, dpi, cancellationToken),
                        cancellationToken);
    }

    private static async Task<RenderedPage> RenderPixelsLockedAsync(
        string filePath, int pageIndex, double dpi, CancellationToken cancellationToken)
    {
        // Zwei Pruefungen, nicht eine. Hier der billige Gewinn: ein bereits
        // abbestellter Auftrag soll die Sperre gar nicht erst nehmen.
        cancellationToken.ThrowIfCancellationRequested();

        // WaitAsync mit Token, damit auch das Warten selbst endet -- sonst
        // haengt ein abbestellter Render weiter hinter allen vor ihm.
        await RenderLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Und noch einmal: zwischen Anstellen und Drankommen vergeht
            // beliebig viel Zeit, und genau in der wird abbestellt.
            cancellationToken.ThrowIfCancellationRequested();
            return RenderPixels(filePath, pageIndex, dpi);
        }
        finally
        {
            RenderLock.Release();
        }
    }

    /// <summary>Nur unter <see cref="RenderLock"/> aufrufen.</summary>
    private static RenderedPage RenderPixels(string filePath, int pageIndex, double dpi)
    {
        // Die Seite wird in ein quadratisches Feld von dpi*14 Pixeln eingepasst,
        // Seitenverhaeltnis bleibt. US Legal (14 Zoll) ist das groesste gaengige
        // Format, dpi*14 deckt also die ueblichen Seiten ab. Das ist genau die
        // Rechnung, die Docnet mit PageDimensions(maxDim, maxDim) gemacht hat:
        // Faktor = min(maxDim / kurze Seite, maxDim / lange Seite), Pixelmasse
        // abgeschnitten, nicht gerundet. Die Seitengroesse von PDFium
        // beruecksichtigt /Rotate bereits.
        int maxDim = (int)(dpi * 14);

        var document = Pdfium.OpenDocument(filePath);
        try
        {
            var page = Pdfium.FPDF_LoadPage(document, pageIndex);
            if (page == IntPtr.Zero)
                throw new PdfiumException($"Seite {pageIndex + 1} lässt sich nicht laden.");
            try
            {
                double pageWidth = Pdfium.FPDF_GetPageWidth(page);
                double pageHeight = Pdfium.FPDF_GetPageHeight(page);
                double scale = Math.Min(maxDim / Math.Min(pageWidth, pageHeight),
                                        maxDim / Math.Max(pageWidth, pageHeight));
                int width = (int)(pageWidth * scale);
                int height = (int)(pageHeight * scale);

                // alpha 1: BGRA, transparent vorbelegt, wie bisher.
                var bitmap = Pdfium.FPDFBitmap_Create(width, height, 1);
                if (bitmap == IntPtr.Zero)
                    throw new PdfiumException($"Für Seite {pageIndex + 1} fehlt der Speicher zum Rendern.");
                try
                {
                    var matrix = new Pdfium.FsMatrix { A = (float)scale, D = (float)scale };
                    // Werte genau wie bei Docnet uebernommen (top = height,
                    // bottom = 0), damit das Ergebnis gleich bleibt.
                    var clipping = new Pdfium.FsRectF { Left = 0, Top = height, Right = width, Bottom = 0 };
                    // Flags 0: keine Annotationen, wie bisher GetImage().
                    Pdfium.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, 0);

                    int stride = Pdfium.FPDFBitmap_GetStride(bitmap);
                    var buffer = Pdfium.FPDFBitmap_GetBuffer(bitmap);
                    int rowBytes = width * Pdfium.BytesPerPixel;
                    var pixels = new byte[rowBytes * height];
                    if (stride == rowBytes)
                    {
                        Marshal.Copy(buffer, pixels, 0, pixels.Length);
                    }
                    else
                    {
                        for (int row = 0; row < height; row++)
                            Marshal.Copy(buffer + row * stride, pixels, row * rowBytes, rowBytes);
                    }
                    return new RenderedPage(width, height, pixels);
                }
                finally
                {
                    Pdfium.FPDFBitmap_Destroy(bitmap);
                }
            }
            finally
            {
                Pdfium.FPDF_ClosePage(page);
            }
        }
        finally
        {
            Pdfium.FPDF_CloseDocument(document);
        }
    }

    public void Close()
    {
        _filePath = null;
        _pageCount = 0;
    }
}

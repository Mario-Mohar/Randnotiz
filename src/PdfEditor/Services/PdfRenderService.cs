using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Docnet.Core;
using Docnet.Core.Models;

namespace Randnotiz.Services;

public class PdfRenderService
{
    private string? _filePath;
    private int _pageCount;

    // Ein Platz, weil pdfium hier ohnehin nicht parallelisiert wird: es geht um
    // Sicherheit, nicht um Durchsatz. Statisch, also prozessweit -- das ist
    // richtig, solange es genau eine Bibliotheksinstanz gibt, und genau das ist
    // seit dem Singleton-Fix der Fall.
    private static readonly SemaphoreSlim RenderLock = new(1, 1);

    public int PageCount => _pageCount;

    public async Task LoadAsync(string filePath)
    {
        _filePath = filePath;
        // Dieselbe Sperre wie beim Rendern: GetDocReader greift auch von hier
        // auf dieselbe native Instanz zu, und ein Dokumentwechsel kann auf
        // einen noch laufenden Render des vorigen treffen.
        await RenderLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Kein using: DocLib.Instance ist ein prozessweites Singleton. Wer es
            // hier freigibt, nimmt es auch jedem spaeteren Aufruf weg. Der reader
            // darunter gehoert dagegen sehr wohl pro Dokument freigegeben.
            var library = DocLib.Instance;
            using var reader = library.GetDocReader(filePath, new PageDimensions(1, 1));
            _pageCount = reader.GetPageCount();
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
    /// Mitten im nativen Aufruf laesst sich nichts unterbrechen: GetDocReader
    /// und GetImage laufen durch. Die Koernung ist damit eine Seite, und das
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

                // PageDimensions is a max bounding box — the page scales to fit while preserving aspect ratio.
                // US Legal (14 inches) is the largest common page size, so dpi*14 covers standard pages.
                int maxDim = (int)(dpi * 14);

                // Siehe LoadAsync: das Singleton wird nicht pro Seite freigegeben.
                var library = DocLib.Instance;
                using var reader = library.GetDocReader(filePath, new PageDimensions(maxDim, maxDim));
                using var page = reader.GetPageReader(pageIndex);

                int width = page.GetPageWidth();
                int height = page.GetPageHeight();
                byte[] rawBytes = page.GetImage(); // Docnet returns BGRA, no row padding

                var bmp = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(dpi, dpi),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                using var fb = bmp.Lock();
                int sourceStride = width * 4;
                if (fb.RowBytes == sourceStride)
                {
                    Marshal.Copy(rawBytes, 0, fb.Address, rawBytes.Length);
                }
                else
                {
                    for (int row = 0; row < height; row++)
                        Marshal.Copy(rawBytes, row * sourceStride, fb.Address + row * fb.RowBytes, sourceStride);
                }

                return (Bitmap)bmp;
            }
            finally
            {
                RenderLock.Release();
            }
        }, cancellationToken);
    }

    public void Close()
    {
        _filePath = null;
        _pageCount = 0;
    }
}

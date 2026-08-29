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

    public int PageCount => _pageCount;

    public Task LoadAsync(string filePath)
    {
        _filePath = filePath;
        // Kein using: DocLib.Instance ist ein prozessweites Singleton. Wer es
        // hier freigibt, nimmt es auch jedem spaeteren Aufruf weg. Der reader
        // darunter gehoert dagegen sehr wohl pro Dokument freigegeben.
        var library = DocLib.Instance;
        using var reader = library.GetDocReader(filePath, new PageDimensions(1, 1));
        _pageCount = reader.GetPageCount();
        return Task.CompletedTask;
    }

    public Task<Bitmap> RenderPageAsync(int pageIndex, double dpi = 150)
    {
        if (_filePath is null) throw new InvalidOperationException("No PDF loaded.");
        var filePath = _filePath; // capture before entering background thread

        return Task.Run(() =>
        {
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
        });
    }

    public void Close()
    {
        _filePath = null;
        _pageCount = 0;
    }
}

using System.IO;
using Avalonia.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;

namespace Randnotiz.Services;

public class PdfRenderService
{
    private string? _filePath;
    private int _pageCount;

    public int PageCount => _pageCount;

    public Task LoadAsync(string filePath)
    {
        _filePath = filePath;

        using var library = DocLib.Instance;
        using var reader = library.GetDocReader(filePath, new PageDimensions(1, 1));
        _pageCount = reader.GetPageCount();

        return Task.CompletedTask;
    }

    public (double Width, double Height) GetPageSize(int pageIndex)
    {
        if (_filePath is null) throw new InvalidOperationException("No PDF loaded.");

        using var library = DocLib.Instance;
        using var reader = library.GetDocReader(_filePath, new PageDimensions(1, 1));
        using var page = reader.GetPageReader(pageIndex);
        return (page.GetPageWidth(), page.GetPageHeight());
    }

    public Task<Bitmap> RenderPageAsync(int pageIndex, double dpi = 150)
    {
        if (_filePath is null) throw new InvalidOperationException("No PDF loaded.");

        // PageDimensions is a max bounding box — the page scales to fit while preserving aspect ratio.
        // US Legal (14 inches) is the largest common page size, so dpi*14 covers standard pages.
        int maxDim = (int)(dpi * 14);

        using var library = DocLib.Instance;
        using var reader = library.GetDocReader(_filePath, new PageDimensions(maxDim, maxDim));
        using var page = reader.GetPageReader(pageIndex);

        int width = page.GetPageWidth();
        int height = page.GetPageHeight();
        var rawBytes = page.GetImage();

        // Docnet returns BGRA format
        using var skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, skBitmap.GetPixels(), rawBytes.Length);

        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;

        var bitmap = new Bitmap(ms);
        return Task.FromResult(bitmap);
    }

    public void Close()
    {
        _filePath = null;
        _pageCount = 0;
    }
}

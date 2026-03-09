using System.IO;
using Avalonia.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;

namespace PdfEditor.Services;

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

        // Get page dimensions at native size to calculate scaling
        int scaledWidth, scaledHeight;
        using (var lib = DocLib.Instance)
        using (var sizeReader = lib.GetDocReader(_filePath, new PageDimensions(1, 1)))
        using (var sizePage = sizeReader.GetPageReader(pageIndex))
        {
            double nativeWidth = sizePage.GetPageWidth();
            double nativeHeight = sizePage.GetPageHeight();
            scaledWidth = (int)(nativeWidth * dpi / 72.0);
            scaledHeight = (int)(nativeHeight * dpi / 72.0);
        }

        // Render at target resolution
        using var library = DocLib.Instance;
        using var reader = library.GetDocReader(_filePath, new PageDimensions(scaledWidth, scaledHeight));
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

using System.Runtime.InteropServices;

namespace Randnotiz.Services;

/// <summary>
/// Duenne Anbindung an PDFium. Die Binaries kommen aus den Paketen
/// bblanchon.PDFium.Linux und bblanchon.PDFium.Win32 (libpdfium.so und
/// pdfium.dll unter runtimes/*/native), die Signaturen stammen aus dem
/// fpdfview.h, das in denselben Paketen liegt.
/// </summary>
/// <remarks>
/// PDFium ist nicht threadsicher. Jeder Aufruf hier muss unter der
/// RenderLock von <see cref="PdfRenderService"/> laufen, auch das
/// Initialisieren.
/// </remarks>
internal static class Pdfium
{
    private const string Lib = "pdfium";

    // FPDFBitmap_Create(..., alpha: 1) liefert BGRA, vier Byte je Pixel.
    internal const int BytesPerPixel = 4;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FsMatrix
    {
        public float A, B, C, D, E, F;
    }

    // Reihenfolge wie in fpdfview.h: left, top, right, bottom.
    [StructLayout(LayoutKind.Sequential)]
    internal struct FsRectF
    {
        public float Left, Top, Right, Bottom;
    }

    [DllImport(Lib)] private static extern void FPDF_InitLibrary();

    // Der Pfad ist laut fpdfview.h UTF-8, auf jedem System.
    [DllImport(Lib)]
    private static extern IntPtr FPDF_LoadDocument(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filePath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    // unsigned long: 32 Bit unter Windows, 64 Bit unter Linux.
    [DllImport(Lib)] private static extern CULong FPDF_GetLastError();
    [DllImport(Lib)] internal static extern void FPDF_CloseDocument(IntPtr document);
    [DllImport(Lib)] internal static extern int FPDF_GetPageCount(IntPtr document);
    [DllImport(Lib)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);
    [DllImport(Lib)] internal static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Lib)] internal static extern double FPDF_GetPageWidth(IntPtr page);
    [DllImport(Lib)] internal static extern double FPDF_GetPageHeight(IntPtr page);
    [DllImport(Lib)] internal static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
    [DllImport(Lib)] internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    [DllImport(Lib)] internal static extern int FPDFBitmap_GetStride(IntPtr bitmap);
    [DllImport(Lib)] internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

    [DllImport(Lib)]
    internal static extern void FPDF_RenderPageBitmapWithMatrix(
        IntPtr bitmap, IntPtr page, in FsMatrix matrix, in FsRectF clipping, int flags);

    private static bool _initialized;

    /// <summary>Einmal pro Prozess, nie wieder freigegeben: wie vorher das
    /// Singleton DocLib.Instance aus Docnet.</summary>
    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        FPDF_InitLibrary();
        _initialized = true;
    }

    /// <summary>Oeffnet ein Dokument. Der Aufrufer schliesst es mit
    /// <see cref="FPDF_CloseDocument"/>.</summary>
    internal static IntPtr OpenDocument(string filePath)
    {
        EnsureInitialized();
        var document = FPDF_LoadDocument(filePath, null);
        if (document == IntPtr.Zero)
            throw new PdfiumException(DescribeLoadError(FPDF_GetLastError().Value));
        return document;
    }

    private static string DescribeLoadError(ulong code) => code switch
    {
        2 => "Die Datei wurde nicht gefunden oder lässt sich nicht öffnen.",
        3 => "Die Datei ist keine gültige PDF oder beschädigt.",
        4 => "Die PDF ist passwortgeschützt.",
        5 => "Die PDF verwendet ein nicht unterstütztes Sicherheitsverfahren.",
        6 => "Eine Seite der PDF ist fehlerhaft.",
        _ => $"Die PDF lässt sich nicht öffnen (PDFium-Fehler {code}).",
    };
}

public sealed class PdfiumException(string message) : Exception(message);

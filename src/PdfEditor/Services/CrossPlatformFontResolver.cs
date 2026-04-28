using System.IO;
using System.Runtime.InteropServices;
using PdfSharp.Fonts;

namespace Randnotiz.Services;

public class CrossPlatformFontResolver : IFontResolver
{
    private static readonly Dictionary<string, string> FontFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = "arial.ttf",
        ["Arial Bold"] = "arialbd.ttf",
        ["Arial Italic"] = "ariali.ttf",
        ["Arial Bold Italic"] = "arialbi.ttf",
        ["Times New Roman"] = "times.ttf",
        ["Times New Roman Bold"] = "timesbd.ttf",
        ["Times New Roman Italic"] = "timesi.ttf",
        ["Times New Roman Bold Italic"] = "timesbi.ttf",
        ["Courier New"] = "cour.ttf",
        ["Courier New Bold"] = "courbd.ttf",
        ["Courier New Italic"] = "couri.ttf",
        ["Courier New Bold Italic"] = "courbi.ttf",
        ["Calibri"] = "calibri.ttf",
        ["Calibri Bold"] = "calibrib.ttf",
        ["Calibri Italic"] = "calibrii.ttf",
        ["Calibri Bold Italic"] = "calibriz.ttf",
        ["Verdana"] = "verdana.ttf",
        ["Verdana Bold"] = "verdanab.ttf",
        ["Verdana Italic"] = "verdanai.ttf",
        ["Verdana Bold Italic"] = "verdanaz.ttf",
        ["Tahoma"] = "tahoma.ttf",
        ["Tahoma Bold"] = "tahomabd.ttf",
    };

    // Linux fallback font mappings (common names on Linux Mint / Ubuntu)
    private static readonly Dictionary<string, string[]> LinuxFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = ["LiberationSans-Regular.ttf", "DejaVuSans.ttf", "FreeSans.ttf"],
        ["Arial Bold"] = ["LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf", "FreeSansBold.ttf"],
        ["Arial Italic"] = ["LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf", "FreeSansOblique.ttf"],
        ["Arial Bold Italic"] = ["LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf", "FreeSansBoldOblique.ttf"],
        ["Times New Roman"] = ["LiberationSerif-Regular.ttf", "DejaVuSerif.ttf", "FreeSerif.ttf"],
        ["Times New Roman Bold"] = ["LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf", "FreeSerifBold.ttf"],
        ["Times New Roman Italic"] = ["LiberationSerif-Italic.ttf", "DejaVuSerif-Italic.ttf", "FreeSerifItalic.ttf"],
        ["Times New Roman Bold Italic"] = ["LiberationSerif-BoldItalic.ttf", "DejaVuSerif-BoldItalic.ttf", "FreeSerifBoldItalic.ttf"],
        ["Courier New"] = ["LiberationMono-Regular.ttf", "DejaVuSansMono.ttf", "FreeMono.ttf"],
        ["Courier New Bold"] = ["LiberationMono-Bold.ttf", "DejaVuSansMono-Bold.ttf", "FreeMonoBold.ttf"],
        ["Courier New Italic"] = ["LiberationMono-Italic.ttf", "DejaVuSansMono-Oblique.ttf", "FreeMonoOblique.ttf"],
        ["Courier New Bold Italic"] = ["LiberationMono-BoldItalic.ttf", "DejaVuSansMono-BoldOblique.ttf", "FreeMonoBoldOblique.ttf"],
        ["Calibri"] = ["LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
        ["Calibri Bold"] = ["LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
        ["Calibri Italic"] = ["LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"],
        ["Calibri Bold Italic"] = ["LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"],
        ["Verdana"] = ["LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
        ["Verdana Bold"] = ["LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
        ["Verdana Italic"] = ["LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"],
        ["Verdana Bold Italic"] = ["LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"],
        ["Tahoma"] = ["LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
        ["Tahoma Bold"] = ["LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
    };

    private static readonly string[] FontDirectories = GetFontDirectories();

    private static string[] GetFontDirectories()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return [Environment.GetFolderPath(Environment.SpecialFolder.Fonts)];
        }

        // Linux font directories
        var dirs = new List<string>
        {
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
        };

        return dirs.Where(Directory.Exists).ToArray();
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var key = familyName;
        if (isBold && isItalic) key += " Bold Italic";
        else if (isBold) key += " Bold";
        else if (isItalic) key += " Italic";

        // Check if we can resolve this font
        if (CanResolve(key))
            return new FontResolverInfo(key);

        // Fallback to base family name
        if (CanResolve(familyName))
            return new FontResolverInfo(familyName);

        // Ultimate fallback to Arial (or its Linux equivalent)
        return new FontResolverInfo("Arial");
    }

    private bool CanResolve(string faceName)
    {
        // Try Windows fonts
        if (FontFiles.ContainsKey(faceName))
        {
            var path = FindFontFile(FontFiles[faceName]);
            if (path is not null) return true;
        }

        // Try Linux fallbacks
        if (LinuxFallbacks.TryGetValue(faceName, out var fallbacks))
        {
            foreach (var fallback in fallbacks)
            {
                var path = FindFontFile(fallback);
                if (path is not null) return true;
            }
        }

        return false;
    }

    public byte[]? GetFont(string faceName)
    {
        // Try Windows font file first
        if (FontFiles.TryGetValue(faceName, out var fileName))
        {
            var path = FindFontFile(fileName);
            if (path is not null)
                return File.ReadAllBytes(path);
        }

        // Try Linux fallback fonts
        if (LinuxFallbacks.TryGetValue(faceName, out var fallbacks))
        {
            foreach (var fallback in fallbacks)
            {
                var path = FindFontFile(fallback);
                if (path is not null)
                    return File.ReadAllBytes(path);
            }
        }

        // Last resort: find any TTF file
        foreach (var dir in FontDirectories)
        {
            try
            {
                var anyFont = Directory.EnumerateFiles(dir, "*.ttf", SearchOption.AllDirectories).FirstOrDefault();
                if (anyFont is not null)
                    return File.ReadAllBytes(anyFont);
            }
            catch { }
        }

        return null;
    }

    private static string? FindFontFile(string fileName)
    {
        foreach (var dir in FontDirectories)
        {
            // Direct path
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path)) return path;

            // Search subdirectories (Linux fonts are often in subdirs)
            try
            {
                var found = Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (found is not null) return found;
            }
            catch { }
        }

        return null;
    }
}

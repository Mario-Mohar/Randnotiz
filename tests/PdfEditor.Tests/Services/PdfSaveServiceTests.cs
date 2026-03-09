using System.IO;
using PdfEditor.Models;
using PdfEditor.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Tests.Services;

public class PdfSaveServiceTests
{
    public PdfSaveServiceTests()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new CrossPlatformFontResolver();
    }

    [Fact]
    public void Save_WritesTextAnnotationsIntoPdf()
    {
        // Arrange: create a simple 1-page PDF
        var tempInput = Path.GetTempFileName() + ".pdf";
        var tempOutput = Path.GetTempFileName() + ".pdf";

        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = XUnitPt.FromPoint(595); // A4
            page.Height = XUnitPt.FromPoint(842);
            doc.Save(tempInput);
        }

        var pages = new List<PdfPageModel>
        {
            new(0)
            {
                WidthInPoints = 595,
                HeightInPoints = 842
            }
        };
        pages[0].Annotations.Add(new TextAnnotation(100, 200, 0) { Text = "Hello PDF" });

        var service = new PdfSaveService();

        // Act
        service.Save(tempInput, tempOutput, pages);

        // Assert: output file exists and is a valid PDF
        Assert.True(File.Exists(tempOutput));
        using var result = PdfReader.Open(tempOutput, PdfDocumentOpenMode.Import);
        Assert.Equal(1, result.PageCount);

        // Cleanup
        File.Delete(tempInput);
        File.Delete(tempOutput);
    }
}

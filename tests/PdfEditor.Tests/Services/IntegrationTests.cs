using System.IO;
using Randnotiz.Models;
using Randnotiz.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Randnotiz.Tests.Services;

public class IntegrationTests
{
    public IntegrationTests()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new CrossPlatformFontResolver();
    }

    [Fact]
    public void FullWorkflow_CreatePdf_AddAnnotations_SaveAndVerify()
    {
        // Arrange: create a 2-page PDF
        var tempInput = Path.GetTempFileName() + ".pdf";
        var tempOutput = Path.GetTempFileName() + ".pdf";

        using (var doc = new PdfDocument())
        {
            var page1 = doc.AddPage();
            page1.Width = XUnitPt.FromPoint(595);
            page1.Height = XUnitPt.FromPoint(842);
            var page2 = doc.AddPage();
            page2.Width = XUnitPt.FromPoint(595);
            page2.Height = XUnitPt.FromPoint(842);
            doc.Save(tempInput);
        }

        // Build page models with annotations
        var pages = new List<PdfPageModel>
        {
            new(0) { WidthInPoints = 595, HeightInPoints = 842 },
            new(1) { WidthInPoints = 595, HeightInPoints = 842 }
        };
        pages[0].Annotations.Add(new TextAnnotation(50, 100, 0) { Text = "Name: Max Mustermann", FontSize = 14 });
        pages[0].Annotations.Add(new TextAnnotation(50, 150, 0) { Text = "Datum: 04.03.2026", FontSize = 12 });
        pages[1].Annotations.Add(new TextAnnotation(50, 100, 1) { Text = "Seite 2 Text" });

        var service = new PdfSaveService();

        // Act
        service.Save(tempInput, tempOutput, pages);

        // Assert
        Assert.True(File.Exists(tempOutput));
        using var result = PdfReader.Open(tempOutput, PdfDocumentOpenMode.Import);
        Assert.Equal(2, result.PageCount);

        var fileSize = new FileInfo(tempOutput).Length;
        var inputSize = new FileInfo(tempInput).Length;
        Assert.True(fileSize > inputSize, "Output should be larger than input due to added text");

        // Cleanup
        File.Delete(tempInput);
        File.Delete(tempOutput);
    }
}

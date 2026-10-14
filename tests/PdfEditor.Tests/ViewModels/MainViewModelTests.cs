using PdfSharp.Fonts;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using Randnotiz.Services;
using Randnotiz.Tests.Services;
using Randnotiz.ViewModels;

namespace Randnotiz.Tests.ViewModels;

/// <summary>
/// Der Weg durch das ViewModel, so wie die Oberflaeche ihn geht, nur ohne
/// Klicks: Oeffnen, Text setzen, zoomen, speichern. Laeuft in der
/// Headless-Sitzung, weil das Rendern Avalonia-Bitmaps baut.
/// </summary>
[Collection(PdfiumCollection.Name)]
public class MainViewModelTests
{
    public MainViewModelTests()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new CrossPlatformFontResolver();
    }

    private static Task<T> OnUi<T>(Func<Task<T>> action) =>
        HeadlessTestApp.Session.Dispatch(action, CancellationToken.None);

    private static async Task WaitForNewImage(MainViewModel vm, object? oldImage)
    {
        for (int i = 0; i < 200 && ReferenceEquals(vm.Pages[0].RenderedImage, oldImage); i++)
            await Task.Delay(20);
        Assert.False(ReferenceEquals(vm.Pages[0].RenderedImage, oldImage), "Zoom hat nicht neu gerendert");
    }

    [Fact]
    public async Task Zoom_AendertWederAnnotationNochGespeicherteGroesse()
    {
        var input = AnnotationPlacementTests.CreateBlankPdf((595, 842, 0));
        var output = Path.Combine(Path.GetDirectoryName(input)!, "aus.pdf");
        try
        {
            var ann = await OnUi(async () =>
            {
                var vm = new MainViewModel();
                await vm.LoadFileAsync(input);
                vm.AddAnnotation(0, 100, 200);
                var annotation = vm.Pages[0].Annotations.Single();
                annotation.Text = "HH";

                var oldImage = vm.Pages[0].RenderedImage;
                vm.ZoomInCommand.Execute(null);
                vm.ZoomInCommand.Execute(null);
                vm.ZoomInCommand.Execute(null); // 200 %
                await WaitForNewImage(vm, oldImage);

                new PdfSaveService().Save(input, output, vm.Pages.ToList());
                return (annotation.X, annotation.Y, annotation.FontSize);
            });

            Assert.Equal((100.0, 200.0, 12.0), ann);

            // Gespeichert wird in Punkten: 12 pt Schrift, Versalhoehe rund 8,6 pt,
            // an derselben Stelle wie bei 100 %.
            var ink = await AnnotationPlacementTests.InkBounds(output);
            Assert.NotNull(ink);
            var (left, top, _, bottom) = ink.Value;
            Assert.InRange(bottom - top, 12 * 0.65, 12 * 0.8);
            Assert.InRange(left, 100, 104);
            Assert.InRange(top, 200, 206);
        }
        finally
        {
            AnnotationPlacementTests.Delete(input);
        }
    }

    /// <summary>Zaehlt die Textoperatoren (Tj, TJ, ' und ") auf einer Seite,
    /// also wie oft Text in den Inhalt geschrieben wurde.</summary>
    private static int CountTextShows(string path, int pageIndex = 0)
    {
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        int Count(CSequence seq) => seq.Sum(o => o switch
        {
            COperator op when op.OpCode.Name is "Tj" or "TJ" or "'" or "\"" => 1,
            CSequence inner => Count(inner),
            _ => 0
        });
        return Count(ContentReader.ReadContent(doc.Pages[pageIndex]));
    }

    [Fact]
    public async Task MehrfachSpeichern_SchreibtJedenTextGenauEinmal()
    {
        // Nachgestellt wie in #18: eintippen, zweimal speichern, dann weiter
        // arbeiten und nochmal speichern.
        var input = AnnotationPlacementTests.CreateBlankPdf((595, 842, 0));
        try
        {
            var (afterFirst, afterSecond, left, selected) = await OnUi(async () =>
            {
                var vm = new MainViewModel();
                await vm.LoadFileAsync(input);
                vm.AddAnnotation(0, 100, 100);
                vm.Pages[0].Annotations.Single().Text = "Erster";

                await vm.SaveToAsync(input);
                int first = CountTextShows(input);
                await vm.SaveToAsync(input);
                int second = CountTextShows(input);
                int remaining = vm.Pages[0].Annotations.Count;
                bool hasSelection = vm.SelectedAnnotation is not null;

                vm.AddAnnotation(0, 300, 500);
                vm.Pages[0].Annotations.Single().Text = "Zweiter";
                await vm.SaveToAsync(input);
                return (first, second, remaining, hasSelection);
            });

            Assert.Equal(1, afterFirst);
            Assert.Equal(1, afterSecond);
            Assert.Equal(0, left);
            Assert.False(selected);
            Assert.Equal(2, CountTextShows(input));
        }
        finally
        {
            AnnotationPlacementTests.Delete(input);
        }
    }

    [Fact]
    public async Task SpeichernUnter_LaesstQuelleUndAnnotationenStehen()
    {
        // Die Quelle bleibt unberuehrt, also bleiben die Annotationen
        // bearbeitbar: das naechste Speichern schreibt sie genau einmal.
        var input = AnnotationPlacementTests.CreateBlankPdf((595, 842, 0));
        var other = Path.Combine(Path.GetDirectoryName(input)!, "kopie.pdf");
        try
        {
            int remaining = await OnUi(async () =>
            {
                var vm = new MainViewModel();
                await vm.LoadFileAsync(input);
                vm.AddAnnotation(0, 100, 100);
                vm.Pages[0].Annotations.Single().Text = "Erster";
                await vm.SaveToAsync(other);
                return vm.Pages[0].Annotations.Count;
            });

            Assert.Equal(1, remaining);
            Assert.Equal(0, CountTextShows(input));
            Assert.Equal(1, CountTextShows(other));
        }
        finally
        {
            AnnotationPlacementTests.Delete(input);
        }
    }

    [Fact]
    public async Task GedrehteSeite_TextLandetObenLinks()
    {
        // #20 nachgestellt: A4 hoch mit /Rotate 90 oeffnen, oben links Text
        // setzen, speichern und neu laden.
        var input = AnnotationPlacementTests.CreateBlankPdf((595, 842, 90));
        try
        {
            var (width, height) = await OnUi(async () =>
            {
                var vm = new MainViewModel();
                await vm.LoadFileAsync(input);
                vm.AddAnnotation(0, 40, 30);
                vm.Pages[0].Annotations.Single().Text = "Oben links";
                await vm.SaveToAsync(input);
                return (vm.Pages[0].WidthInPoints, vm.Pages[0].HeightInPoints);
            });

            Assert.Equal(842, width, 0.01);
            Assert.Equal(595, height, 0.01);

            var ink = await AnnotationPlacementTests.InkBounds(input);
            Assert.NotNull(ink);
            var (left, top, right, bottom) = ink.Value;
            Assert.InRange(left, 40, 44);
            Assert.InRange(top, 30, 38);
            Assert.True(right - left > bottom - top, $"Text steht quer: {ink}");
        }
        finally
        {
            AnnotationPlacementTests.Delete(input);
        }
    }
}

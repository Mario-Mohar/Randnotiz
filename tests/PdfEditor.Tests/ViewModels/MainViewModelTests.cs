using PdfSharp.Fonts;
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
}

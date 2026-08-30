using System.IO;
using System.Threading.Tasks;
using Randnotiz.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Randnotiz.Tests.Services;

/// <summary>
/// Laden und Rendern greifen beide auf dieselbe native pdfium-Instanz zu.
/// Diese Tests fahren genau das Muster, das die Sperre absichern soll:
/// gleichzeitige Zugriffe auf DocLib.Instance aus mehreren Aufgaben.
///
/// Das Rendern selbst laesst sich hier nicht zu Ende pruefen -- WriteableBitmap
/// braucht eine Avalonia-Renderplattform, die dieses Testprojekt nicht
/// aufsetzt. Geprueft wird deshalb der Teil, an dem die native Bibliothek
/// haengt; die Bitmap dahinter ist reine Speicherkopie.
/// </summary>
public class PdfRenderServiceTests
{
    private static string CreatePdf(int pageCount)
    {
        var path = Path.GetTempFileName() + ".pdf";
        using var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            var page = doc.AddPage();
            page.Width = XUnitPt.FromPoint(595);
            page.Height = XUnitPt.FromPoint(842);
        }
        doc.Save(path);
        return path;
    }

    [Fact]
    public async Task GleichzeitigesLaden_KommtDurch()
    {
        // Ohne Serialisierung greifen diese Aufgaben unabgesichert auf
        // dieselbe pdfium-Instanz zu -- der Fall, den das Issue beschreibt.
        var paths = new List<string>();
        for (int i = 1; i <= 6; i++) paths.Add(CreatePdf(i));

        try
        {
            var services = new List<PdfRenderService>();
            var tasks = new List<Task>();
            foreach (var path in paths)
            {
                var service = new PdfRenderService();
                services.Add(service);
                var captured = path;
                tasks.Add(Task.Run(() => service.LoadAsync(captured)));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < services.Count; i++)
                Assert.Equal(i + 1, services[i].PageCount);
        }
        finally
        {
            foreach (var path in paths) File.Delete(path);
        }
    }

    [Fact]
    public async Task WiederholtesLadenDesselbenDokuments_BleibtStabil()
    {
        // Ein Dokumentwechsel kann auf einen noch laufenden Zugriff treffen;
        // dieselbe Instanz mehrfach hintereinander und nebeneinander zu laden
        // ist die kuerzeste Form davon.
        var path = CreatePdf(3);
        try
        {
            var service = new PdfRenderService();
            var tasks = new List<Task>();
            for (int i = 0; i < 8; i++)
                tasks.Add(Task.Run(() => service.LoadAsync(path)));

            await Task.WhenAll(tasks);
            Assert.Equal(3, service.PageCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Die private Sperre, um das Anstehen in der Warteschlange
    /// ueberhaupt herstellen zu koennen.</summary>
    private static SemaphoreSlim RenderLock =>
        (SemaphoreSlim)typeof(PdfRenderService)
            .GetField("RenderLock", System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    public async Task BereitsAbbestellterAuftrag_NimmtDieSperreGarNichtErst()
    {
        var path = CreatePdf(1);
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.RenderPageAsync(0, 72, cts.Token));

            // Haette der Auftrag die Sperre genommen und nicht zurueckgegeben,
            // stuende hier alles Weitere fuer immer an.
            Assert.Equal(1, RenderLock.CurrentCount);
            await service.LoadAsync(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AbbestellenWaehrendDesWartens_LaesstDenAuftragLos()
    {
        // Der eigentliche Gewinn: wer in der Warteschlange steht, soll sie
        // verlassen koennen, statt hinter allen vor ihm auszuharren.
        var path = CreatePdf(1);
        var service = new PdfRenderService();
        // Erst laden: LoadAsync braucht dieselbe Sperre, die gleich belegt wird.
        await service.LoadAsync(path);

        await RenderLock.WaitAsync();
        try
        {
            using var cts = new CancellationTokenSource();
            var render = service.RenderPageAsync(0, 72, cts.Token);

            await Task.Delay(50);
            Assert.False(render.IsCompleted, "der Auftrag muss noch anstehen");

            cts.Cancel();

            // Mit Frist: griffe das Abbestellen nicht, wuerde der Auftrag hier
            // ewig anstehen und die Sperre nie zurueckgeben -- ein haengender
            // Testlauf statt eines fehlgeschlagenen. Genau das passiert, wenn
            // WaitAsync ohne Token aufgerufen wird.
            var finished = await Task.WhenAny(render, Task.Delay(5000));
            Assert.True(finished == render, "das Abbestellen muss die Warteschlange verlassen");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render);
        }
        finally
        {
            RenderLock.Release();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RenderOhneGeladenesDokument_Wirft()
    {
        var service = new PdfRenderService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RenderPageAsync(0));
    }

    [Fact]
    public async Task NachCloseIstNichtsMehrGeladen()
    {
        var path = CreatePdf(2);
        try
        {
            var service = new PdfRenderService();
            await service.LoadAsync(path);
            Assert.Equal(2, service.PageCount);

            service.Close();
            Assert.Equal(0, service.PageCount);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RenderPageAsync(0));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

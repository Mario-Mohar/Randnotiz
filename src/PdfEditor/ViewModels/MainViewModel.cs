using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Randnotiz.Models;
using Randnotiz.Services;

namespace Randnotiz.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly PdfRenderService _renderService = new();
    private readonly PdfSaveService _saveService = new();

    private string? _currentFilePath;
    private double _selectedFontSize = 12.0;
    private string _selectedFontFamily = "Arial";
    private TextAnnotation? _selectedAnnotation;
    private double _zoomLevel = 1.0;

    private const double BaseDpi = 96.0;
    private static readonly double[] ZoomLevels = [1.0, 1.25, 1.5, 2.0, 3.0];

    private double CurrentDpi => BaseDpi * _zoomLevel;

    // Nur der jeweils letzte Auftrag zaehlt. Fuenfmal auf Zoom+ geklickt hiess
    // frueher: fuenf vollstaendige Neurendern des Dokuments hintereinander, und
    // erst das letzte zeigte die eingestellte Stufe.
    private CancellationTokenSource? _renderCts;

    // Absichtlich ohne Dispose auf der alten Quelle: Auftraege, die den Token
    // noch halten, wuerden sonst auf einer entsorgten Quelle registrieren. Eine
    // CancellationTokenSource ohne Zeitgeber ist billig genug, um sie dem
    // Aufraeumer zu ueberlassen.
    private CancellationToken StartRenderBatch()
    {
        _renderCts?.Cancel();
        _renderCts = new CancellationTokenSource();
        return _renderCts.Token;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Properties
    // ──────────────────────────────────────────────────────────────────────────

    public ObservableCollection<PdfPageModel> Pages { get; } = new();

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (!SetProperty(ref _currentFilePath, value)) return;
            SaveCommand.RaiseCanExecuteChanged();
            SaveAsCommand.RaiseCanExecuteChanged();
        }
    }

    public double SelectedFontSize
    {
        get => _selectedFontSize;
        set => SetProperty(ref _selectedFontSize, value);
    }

    public string SelectedFontFamily
    {
        get => _selectedFontFamily;
        set => SetProperty(ref _selectedFontFamily, value);
    }

    public TextAnnotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (_selectedAnnotation == value) return;
            if (_selectedAnnotation is not null) _selectedAnnotation.IsSelected = false;
            _selectedAnnotation = value;
            if (_selectedAnnotation is not null) _selectedAnnotation.IsSelected = true;
            OnPropertyChanged();
            DeleteAnnotationCommand.RaiseCanExecuteChanged();
        }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        private set
        {
            double oldDpi = CurrentDpi;
            if (!SetProperty(ref _zoomLevel, value)) return;
            OnPropertyChanged(nameof(ZoomText));
            ZoomInCommand.RaiseCanExecuteChanged();
            ZoomOutCommand.RaiseCanExecuteChanged();
            _ = RerenderAllPagesAsync(oldDpi, CurrentDpi);
        }
    }

    public string ZoomText => $"{(int)(_zoomLevel * 100)}%";

    public double[] FontSizes { get; } =
        [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];

    public string[] FontFamilies { get; } =
        ["Arial", "Times New Roman", "Courier New", "Calibri", "Verdana", "Tahoma"];

    // ──────────────────────────────────────────────────────────────────────────
    // Commands
    // ──────────────────────────────────────────────────────────────────────────

    public ICommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand DeleteAnnotationCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(async () => await OpenPdfAsync());
        SaveCommand = new RelayCommand(
            async () => await SavePdfAsync(false),
            () => CurrentFilePath is not null);
        SaveAsCommand = new RelayCommand(
            async () => await SavePdfAsync(true),
            () => CurrentFilePath is not null);
        DeleteAnnotationCommand = new RelayCommand(
            DeleteSelectedAnnotation,
            () => SelectedAnnotation is not null && !SelectedAnnotation.IsEditing);
        ZoomInCommand = new RelayCommand(
            () => ZoomLevel = ZoomLevels.First(z => z > _zoomLevel),
            () => _zoomLevel < ZoomLevels.Last());
        ZoomOutCommand = new RelayCommand(
            () => ZoomLevel = ZoomLevels.Last(z => z < _zoomLevel),
            () => _zoomLevel > ZoomLevels.First());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // File operations
    // ──────────────────────────────────────────────────────────────────────────

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private async Task OpenPdfAsync()
    {
        var window = GetMainWindow();
        if (window is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "PDF öffnen",
            FileTypeFilter =
            [
                new FilePickerFileType("PDF Dateien") { Patterns = ["*.pdf"] }
            ],
            AllowMultiple = false
        });

        if (files.Count == 0) return;
        var filePath = files[0].TryGetLocalPath();
        if (filePath is null) return;

        await LoadFileAsync(filePath);
    }

    // Called by drag-drop in MainWindow
    public async Task LoadFileAsync(string filePath)
    {
        var window = GetMainWindow();

        // Bestellt gleich die Auftraege des vorigen Dokuments ab: sonst wartet
        // das Laden hinter Bildern, die niemand mehr sehen wird.
        var token = StartRenderBatch();

        try
        {
            _renderService.Close();
            Pages.Clear();
            SelectedAnnotation = null;

            await _renderService.LoadAsync(filePath);
            CurrentFilePath = filePath;

            for (int i = 0; i < _renderService.PageCount; i++)
            {
                var image = await _renderService.RenderPageAsync(i, CurrentDpi, token);
                Pages.Add(new PdfPageModel(i)
                {
                    WidthInPoints = image.PixelSize.Width,
                    HeightInPoints = image.PixelSize.Height,
                    RenderedImage = image
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Ein weiteres Dokument wurde geoeffnet, waehrend dieses noch lud.
            // Kein Fehler, und schon gar keiner fuer einen Dialog.
        }
        catch (Exception ex)
        {
            if (window is not null)
                await ShowErrorAsync(window, $"Fehler beim Öffnen der PDF:\n{ex.Message}");
        }
    }

    private async Task SavePdfAsync(bool saveAs)
    {
        if (CurrentFilePath is null) return;
        var window = GetMainWindow();
        if (window is null) return;

        string outputPath = CurrentFilePath;

        if (saveAs)
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "PDF speichern unter",
                DefaultExtension = "pdf",
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Dateien") { Patterns = ["*.pdf"] }
                ],
                SuggestedFileName = System.IO.Path.GetFileName(CurrentFilePath)
            });
            if (file is null) return;
            var path = file.TryGetLocalPath();
            if (path is null) return;
            outputPath = path;
        }

        try
        {
            string tempPath = outputPath + ".tmp";
            _saveService.Save(CurrentFilePath, tempPath, Pages.ToList());

            if (outputPath == CurrentFilePath)
                _renderService.Close();

            if (System.IO.File.Exists(outputPath))
                System.IO.File.Delete(outputPath);
            System.IO.File.Move(tempPath, outputPath);

            if (outputPath == CurrentFilePath)
            {
                var token = StartRenderBatch();
                await _renderService.LoadAsync(outputPath);
                for (int i = 0; i < Pages.Count; i++)
                {
                    var image = await _renderService.RenderPageAsync(i, CurrentDpi, token);
                    Pages[i].RenderedImage = image;
                    Pages[i].WidthInPoints = image.PixelSize.Width;
                    Pages[i].HeightInPoints = image.PixelSize.Height;
                }
            }

            await ShowInfoAsync(window, "PDF erfolgreich gespeichert.");
        }
        catch (OperationCanceledException)
        {
            // Gespeichert ist gespeichert; nur die Vorschau wurde abbestellt.
            await ShowInfoAsync(window, "PDF erfolgreich gespeichert.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(window, $"Fehler beim Speichern:\n{ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Annotations
    // ──────────────────────────────────────────────────────────────────────────

    public void AddAnnotation(int pageIndex, double x, double y)
    {
        if (pageIndex < 0 || pageIndex >= Pages.Count) return;

        var annotation = new TextAnnotation(x, y, pageIndex)
        {
            FontSize = SelectedFontSize,
            FontFamily = SelectedFontFamily,
            IsEditing = true
        };

        Pages[pageIndex].Annotations.Add(annotation);
        SelectedAnnotation = annotation;
    }

    private void DeleteSelectedAnnotation()
    {
        if (SelectedAnnotation is null) return;
        var page = Pages.FirstOrDefault(p => p.PageIndex == SelectedAnnotation.PageIndex);
        page?.Annotations.Remove(SelectedAnnotation);
        SelectedAnnotation = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Zoom
    // ──────────────────────────────────────────────────────────────────────────

    private async Task RerenderAllPagesAsync(double oldDpi, double newDpi)
    {
        if (Pages.Count == 0) return;

        // Erst alle Annotationen skalieren, dann erst rendern.
        //
        // Frueher lag beides in derselben Schleife. Ein Abbruch mittendrin
        // haette die Annotationen der ersten Seiten skaliert und die der
        // uebrigen nicht -- das waeren keine falschen Pixel mehr, sondern
        // verschobene Daten. Die Skalierung ist reine Rechnung ohne
        // Dateizugriff und laeuft deshalb vollstaendig durch, bevor irgendein
        // Auftrag abbestellt werden kann.
        double scale = newDpi / oldDpi;
        foreach (var page in Pages)
        {
            // Scale annotation positions so they stay at the same relative spot on the page
            foreach (var ann in page.Annotations)
            {
                ann.X *= scale;
                ann.Y *= scale;
            }
        }

        var token = StartRenderBatch();
        try
        {
            for (int i = 0; i < Pages.Count; i++)
            {
                var image = await _renderService.RenderPageAsync(i, newDpi, token);
                Pages[i].RenderedImage = image;
                Pages[i].WidthInPoints = image.PixelSize.Width;
                Pages[i].HeightInPoints = image.PixelSize.Height;
            }
        }
        catch (OperationCanceledException)
        {
            // Ein neuer Zoom hat diesen Durchlauf abbestellt. Kein Fehler --
            // und die Bilder holt der neue Durchlauf ohnehin alle nach.
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dialogs
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task ShowErrorAsync(Window window, string message)
    {
        var dialog = new Window
        {
            Title = "Fehler",
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
                }
            }
        };
        var button = ((StackPanel)dialog.Content).Children[1] as Button;
        button!.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(window);
    }

    private static async Task ShowInfoAsync(Window window, string message)
    {
        var dialog = new Window
        {
            Title = "Gespeichert",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock { Text = message },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
                }
            }
        };
        var button = ((StackPanel)dialog.Content).Children[1] as Button;
        button!.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(window);
    }
}

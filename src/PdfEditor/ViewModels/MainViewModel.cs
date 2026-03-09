using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PdfEditor.Models;
using PdfEditor.Services;

namespace PdfEditor.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly PdfRenderService _renderService = new();
    private readonly PdfSaveService _saveService = new();

    private string? _currentFilePath;
    private double _selectedFontSize = 12.0;
    private string _selectedFontFamily = "Arial";
    private TextAnnotation? _selectedAnnotation;

    public ObservableCollection<PdfPageModel> Pages { get; } = new();

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set => SetProperty(ref _currentFilePath, value);
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
        set => SetProperty(ref _selectedAnnotation, value);
    }

    public double[] FontSizes { get; } =
        [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];

    public string[] FontFamilies { get; } =
        ["Arial", "Times New Roman", "Courier New", "Calibri", "Verdana", "Tahoma"];

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand DeleteAnnotationCommand { get; }

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(async () => await OpenPdfAsync());
        SaveCommand = new RelayCommand(
            async () => await SavePdfAsync(false),
            () => CurrentFilePath is not null);
        SaveAsCommand = new RelayCommand(
            async () => await SavePdfAsync(true),
            () => CurrentFilePath is not null);
        DeleteAnnotationCommand = new RelayCommand(DeleteSelectedAnnotation,
            () => SelectedAnnotation is not null);
    }

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

        try
        {
            _renderService.Close();
            Pages.Clear();

            await _renderService.LoadAsync(filePath);
            CurrentFilePath = filePath;

            for (int i = 0; i < _renderService.PageCount; i++)
            {
                var image = await _renderService.RenderPageAsync(i);
                var pageModel = new PdfPageModel(i)
                {
                    WidthInPoints = image.PixelSize.Width,
                    HeightInPoints = image.PixelSize.Height,
                    RenderedImage = image
                };
                Pages.Add(pageModel);
            }
        }
        catch (Exception ex)
        {
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
            {
                _renderService.Close();
            }

            if (System.IO.File.Exists(outputPath))
                System.IO.File.Delete(outputPath);
            System.IO.File.Move(tempPath, outputPath);

            if (outputPath == CurrentFilePath)
            {
                await _renderService.LoadAsync(outputPath);
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].RenderedImage = await _renderService.RenderPageAsync(i);
                }
            }

            await ShowInfoAsync(window, "PDF erfolgreich gespeichert.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(window, $"Fehler beim Speichern:\n{ex.Message}");
        }
    }

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

# PDF-Editor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a WPF desktop application that opens PDFs, lets the user place text freely and fill form fields, and saves the result as a new PDF.

**Architecture:** MVVM WPF app using PDFsharp for PDF manipulation and Windows.Data.Pdf for rendering pages as images. Text annotations are stored as model objects and written into the PDF via PDFsharp's XGraphics on save.

**Tech Stack:** .NET 9, WPF, PDFsharp 6.x, Windows.Data.Pdf (WinRT), xUnit for tests

---

### Task 1: Install .NET 9 SDK and Initialize Project

**Files:**
- Create: `PdfEditor.sln`
- Create: `src/PdfEditor/PdfEditor.csproj`
- Create: `src/PdfEditor/App.xaml` + `App.xaml.cs`
- Create: `src/PdfEditor/MainWindow.xaml` + `MainWindow.xaml.cs`
- Create: `tests/PdfEditor.Tests/PdfEditor.Tests.csproj`
- Create: `.gitignore`

**Step 1: Install .NET 9 SDK**

Download and install the .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0
The runtime 9.0.13 is already installed; we need the matching SDK.

Run: `dotnet --version`
Expected: `9.x.xxx`

**Step 2: Initialize git repository**

```bash
cd "/c/Users/MarioMoharSalesVikin/Documents/Private Projekte/PDF-Editor"
git init
```

**Step 3: Create .gitignore**

```gitignore
bin/
obj/
.vs/
*.user
*.suo
*.DotSettings.user
```

**Step 4: Create WPF project**

```bash
dotnet new sln -n PdfEditor -o .
mkdir -p src/PdfEditor
dotnet new wpf -n PdfEditor -o src/PdfEditor --framework net9.0-windows10.0.19041.0
dotnet sln add src/PdfEditor/PdfEditor.csproj
```

**Step 5: Edit PdfEditor.csproj to enable WinRT**

The `.csproj` must target `net9.0-windows10.0.19041.0` for Windows.Data.Pdf access. The `dotnet new wpf` with `--framework` flag should set this, but verify and adjust:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

**Step 6: Add NuGet packages**

```bash
cd src/PdfEditor
dotnet add package PDFsharp --version 6.*
```

**Step 7: Create test project**

```bash
cd "/c/Users/MarioMoharSalesVikin/Documents/Private Projekte/PDF-Editor"
mkdir -p tests/PdfEditor.Tests
dotnet new xunit -n PdfEditor.Tests -o tests/PdfEditor.Tests --framework net9.0-windows10.0.19041.0
dotnet sln add tests/PdfEditor.Tests/PdfEditor.Tests.csproj
dotnet add tests/PdfEditor.Tests/PdfEditor.Tests.csproj reference src/PdfEditor/PdfEditor.csproj
```

**Step 8: Verify build**

```bash
dotnet build
```
Expected: Build succeeded with 0 errors.

**Step 9: Commit**

```bash
git add .gitignore PdfEditor.sln src/ tests/ docs/
git commit -m "chore: scaffold WPF project with PDFsharp and test project"
```

---

### Task 2: Models — TextAnnotation and PdfPageModel

**Files:**
- Create: `src/PdfEditor/Models/TextAnnotation.cs`
- Create: `src/PdfEditor/Models/PdfPageModel.cs`
- Test: `tests/PdfEditor.Tests/Models/TextAnnotationTests.cs`

**Step 1: Write failing test for TextAnnotation**

```csharp
// tests/PdfEditor.Tests/Models/TextAnnotationTests.cs
using PdfEditor.Models;

namespace PdfEditor.Tests.Models;

public class TextAnnotationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var annotation = new TextAnnotation(100.0, 200.0, 0);

        Assert.Equal(100.0, annotation.X);
        Assert.Equal(200.0, annotation.Y);
        Assert.Equal(0, annotation.PageIndex);
        Assert.Equal("", annotation.Text);
        Assert.Equal(12.0, annotation.FontSize);
        Assert.Equal("Arial", annotation.FontFamily);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var annotation = new TextAnnotation(0, 0, 0);
        annotation.Text = "Urlaubsantrag";
        annotation.FontSize = 16.0;
        annotation.FontFamily = "Times New Roman";
        annotation.X = 50.0;
        annotation.Y = 75.0;

        Assert.Equal("Urlaubsantrag", annotation.Text);
        Assert.Equal(16.0, annotation.FontSize);
        Assert.Equal("Times New Roman", annotation.FontFamily);
        Assert.Equal(50.0, annotation.X);
        Assert.Equal(75.0, annotation.Y);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/PdfEditor.Tests --filter "TextAnnotationTests" -v n
```
Expected: FAIL — `TextAnnotation` does not exist.

**Step 3: Implement TextAnnotation**

```csharp
// src/PdfEditor/Models/TextAnnotation.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfEditor.Models;

public class TextAnnotation : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private string _text = "";
    private double _fontSize = 12.0;
    private string _fontFamily = "Arial";
    private bool _isEditing;

    public TextAnnotation(double x, double y, int pageIndex)
    {
        _x = x;
        _y = y;
        PageIndex = pageIndex;
    }

    public int PageIndex { get; }

    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { _fontFamily = value; OnPropertyChanged(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/PdfEditor.Tests --filter "TextAnnotationTests" -v n
```
Expected: 2 passed.

**Step 5: Create PdfPageModel**

```csharp
// src/PdfEditor/Models/PdfPageModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace PdfEditor.Models;

public class PdfPageModel : INotifyPropertyChanged
{
    private BitmapImage? _renderedImage;

    public int PageIndex { get; }
    public double WidthInPoints { get; set; }
    public double HeightInPoints { get; set; }
    public ObservableCollection<TextAnnotation> Annotations { get; } = new();

    public BitmapImage? RenderedImage
    {
        get => _renderedImage;
        set { _renderedImage = value; OnPropertyChanged(); }
    }

    public PdfPageModel(int pageIndex)
    {
        PageIndex = pageIndex;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add TextAnnotation and PdfPageModel"
```

---

### Task 3: MVVM Infrastructure — RelayCommand and ViewModelBase

**Files:**
- Create: `src/PdfEditor/ViewModels/ViewModelBase.cs`
- Create: `src/PdfEditor/ViewModels/RelayCommand.cs`

**Step 1: Create ViewModelBase**

```csharp
// src/PdfEditor/ViewModels/ViewModelBase.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfEditor.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
```

**Step 2: Create RelayCommand**

```csharp
// src/PdfEditor/ViewModels/RelayCommand.cs
using System.Windows.Input;

namespace PdfEditor.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
```

**Step 3: Build to verify**

```bash
dotnet build src/PdfEditor
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add MVVM infrastructure (ViewModelBase, RelayCommand)"
```

---

### Task 4: PdfRenderService — Render PDF Pages to Images

**Files:**
- Create: `src/PdfEditor/Services/PdfRenderService.cs`

**Step 1: Implement PdfRenderService**

This service uses Windows.Data.Pdf (WinRT) to render PDF pages as BitmapImages for WPF display.

```csharp
// src/PdfEditor/Services/PdfRenderService.cs
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PdfEditor.Services;

public class PdfRenderService
{
    private PdfDocument? _pdfDocument;

    public int PageCount => (int)(_pdfDocument?.PageCount ?? 0);

    public async Task LoadAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        _pdfDocument = await PdfDocument.LoadFromFileAsync(file);
    }

    public (double Width, double Height) GetPageSize(int pageIndex)
    {
        if (_pdfDocument is null) throw new InvalidOperationException("No PDF loaded.");
        using var page = _pdfDocument.GetPage((uint)pageIndex);
        return (page.Size.Width, page.Size.Height);
    }

    public async Task<BitmapImage> RenderPageAsync(int pageIndex, double dpi = 150)
    {
        if (_pdfDocument is null) throw new InvalidOperationException("No PDF loaded.");

        using var page = _pdfDocument.GetPage((uint)pageIndex);
        var stream = new InMemoryRandomAccessStream();

        var options = new PdfPageRenderOptions
        {
            DestinationWidth = (uint)(page.Size.Width * dpi / 72.0),
            DestinationHeight = (uint)(page.Size.Height * dpi / 72.0)
        };

        await page.RenderToStreamAsync(stream, options);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream.AsStream();
        image.EndInit();
        image.Freeze();

        return image;
    }

    public void Close()
    {
        _pdfDocument = null;
    }
}
```

**Step 2: Build to verify WinRT interop compiles**

```bash
dotnet build src/PdfEditor
```
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add PdfRenderService using Windows.Data.Pdf"
```

---

### Task 5: PdfSaveService — Write Annotations into PDF

**Files:**
- Create: `src/PdfEditor/Services/PdfSaveService.cs`
- Test: `tests/PdfEditor.Tests/Services/PdfSaveServiceTests.cs`

**Step 1: Write failing test**

```csharp
// tests/PdfEditor.Tests/Services/PdfSaveServiceTests.cs
using PdfEditor.Models;
using PdfEditor.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Tests.Services;

public class PdfSaveServiceTests
{
    [Fact]
    public void Save_WritesTextAnnotationsIntoPdf()
    {
        // Arrange: create a simple 1-page PDF
        var tempInput = Path.GetTempFileName() + ".pdf";
        var tempOutput = Path.GetTempFileName() + ".pdf";

        using (var doc = new PdfSharp.Pdf.PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = 595; // A4
            page.Height = 842;
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
        using var result = PdfReader.Open(tempOutput, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(1, result.PageCount);

        // Cleanup
        File.Delete(tempInput);
        File.Delete(tempOutput);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/PdfEditor.Tests --filter "PdfSaveServiceTests" -v n
```
Expected: FAIL — `PdfSaveService` does not exist.

**Step 3: Implement PdfSaveService**

```csharp
// src/PdfEditor/Services/PdfSaveService.cs
using PdfEditor.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Services;

public class PdfSaveService
{
    public void Save(string inputPath, string outputPath, IReadOnlyList<PdfPageModel> pages)
    {
        using var document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);

        foreach (var pageModel in pages)
        {
            if (pageModel.PageIndex >= document.PageCount) continue;
            var pdfPage = document.Pages[pageModel.PageIndex];

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            foreach (var annotation in pageModel.Annotations)
            {
                if (string.IsNullOrEmpty(annotation.Text)) continue;

                var font = new XFont(annotation.FontFamily, annotation.FontSize);

                // Annotation coordinates are in display pixels relative to page.
                // Convert from display coordinates to PDF points.
                double scaleX = pdfPage.Width.Point / pageModel.WidthInPoints;
                double scaleY = pdfPage.Height.Point / pageModel.HeightInPoints;

                double pdfX = annotation.X * scaleX;
                double pdfY = annotation.Y * scaleY;

                gfx.DrawString(annotation.Text, font, XBrushes.Black, pdfX, pdfY);
            }
        }

        document.Save(outputPath);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/PdfEditor.Tests --filter "PdfSaveServiceTests" -v n
```
Expected: 1 passed.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add PdfSaveService to write text annotations into PDF"
```

---

### Task 6: MainViewModel — Core Application Logic

**Files:**
- Create: `src/PdfEditor/ViewModels/MainViewModel.cs`

**Step 1: Implement MainViewModel**

```csharp
// src/PdfEditor/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
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

    private async Task OpenPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Dateien (*.pdf)|*.pdf",
            Title = "PDF öffnen"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            _renderService.Close();
            Pages.Clear();

            await _renderService.LoadAsync(dialog.FileName);
            CurrentFilePath = dialog.FileName;

            for (int i = 0; i < _renderService.PageCount; i++)
            {
                var (width, height) = _renderService.GetPageSize(i);
                var pageModel = new PdfPageModel(i)
                {
                    WidthInPoints = width,
                    HeightInPoints = height,
                    RenderedImage = await _renderService.RenderPageAsync(i)
                };
                Pages.Add(pageModel);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Öffnen der PDF:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SavePdfAsync(bool saveAs)
    {
        if (CurrentFilePath is null) return;

        string outputPath = CurrentFilePath;

        if (saveAs)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF Dateien (*.pdf)|*.pdf",
                Title = "PDF speichern unter",
                FileName = System.IO.Path.GetFileName(CurrentFilePath)
            };
            if (dialog.ShowDialog() != true) return;
            outputPath = dialog.FileName;
        }

        try
        {
            // If saving to same file, use a temp file
            string tempPath = outputPath + ".tmp";
            _saveService.Save(CurrentFilePath, tempPath, Pages.ToList());

            // Close render service before overwriting
            if (outputPath == CurrentFilePath)
            {
                _renderService.Close();
            }

            if (System.IO.File.Exists(outputPath))
                System.IO.File.Delete(outputPath);
            System.IO.File.Move(tempPath, outputPath);

            // Reload if we overwrote the current file
            if (outputPath == CurrentFilePath)
            {
                await _renderService.LoadAsync(outputPath);
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].RenderedImage = await _renderService.RenderPageAsync(i);
                }
            }

            MessageBox.Show("PDF erfolgreich gespeichert.", "Gespeichert",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
```

**Step 2: Build to verify**

```bash
dotnet build src/PdfEditor
```
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MainViewModel with open/save/annotation logic"
```

---

### Task 7: UI — MainWindow with Toolbar and Page Display

**Files:**
- Modify: `src/PdfEditor/MainWindow.xaml`
- Modify: `src/PdfEditor/MainWindow.xaml.cs`
- Create: `src/PdfEditor/Views/PageView.xaml`
- Create: `src/PdfEditor/Views/PageView.xaml.cs`

**Step 1: Create PageView UserControl**

```xml
<!-- src/PdfEditor/Views/PageView.xaml -->
<UserControl x:Class="PdfEditor.Views.PageView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:PdfEditor.Models">
    <Grid Margin="0,10">
        <!-- Page container -->
        <Border BorderBrush="#CCCCCC" BorderThickness="1" Background="White"
                HorizontalAlignment="Center" Drop="Border_Drop" AllowDrop="True">
            <Grid>
                <!-- Rendered PDF page image -->
                <Image Source="{Binding RenderedImage}" Stretch="None"
                       RenderOptions.BitmapScalingMode="HighQuality"/>

                <!-- Canvas overlay for text annotations -->
                <Canvas x:Name="AnnotationCanvas"
                        Background="Transparent"
                        MouseLeftButtonDown="Canvas_MouseLeftButtonDown">
                    <ItemsControl ItemsSource="{Binding Annotations}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <Canvas/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemContainerStyle>
                            <Style TargetType="ContentPresenter">
                                <Setter Property="Canvas.Left" Value="{Binding X}"/>
                                <Setter Property="Canvas.Top" Value="{Binding Y}"/>
                            </Style>
                        </ItemsControl.ItemContainerStyle>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type models:TextAnnotation}">
                                <Grid MouseLeftButtonDown="Annotation_MouseLeftButtonDown"
                                      MouseMove="Annotation_MouseMove"
                                      MouseLeftButtonUp="Annotation_MouseLeftButtonUp">
                                    <!-- Display mode: show text -->
                                    <TextBlock Text="{Binding Text}"
                                               FontSize="{Binding FontSize}"
                                               FontFamily="{Binding FontFamily}"
                                               Foreground="Black"
                                               Cursor="SizeAll"
                                               MouseLeftButtonDown="TextBlock_MouseLeftButtonDown"
                                               Visibility="{Binding IsEditing, Converter={StaticResource InverseBoolToVisibility}}"/>
                                    <!-- Edit mode: text input -->
                                    <TextBox Text="{Binding Text, UpdateSourceTrigger=PropertyChanged}"
                                             FontSize="{Binding FontSize}"
                                             FontFamily="{Binding FontFamily}"
                                             BorderThickness="1"
                                             BorderBrush="#4A90D9"
                                             Background="Transparent"
                                             MinWidth="50"
                                             Padding="2"
                                             LostFocus="TextBox_LostFocus"
                                             KeyDown="TextBox_KeyDown"
                                             Loaded="TextBox_Loaded"
                                             Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisibility}}"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Canvas>
            </Grid>
        </Border>
        <!-- Page number -->
        <TextBlock HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,-20"
                   Foreground="Gray" FontSize="11">
            <Run Text="Seite "/>
            <Run Text="{Binding PageIndex, Mode=OneWay, Converter={StaticResource PageNumberConverter}}"/>
        </TextBlock>
    </Grid>
</UserControl>
```

**Step 2: Create PageView code-behind**

```csharp
// src/PdfEditor/Views/PageView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PdfEditor.Models;
using PdfEditor.ViewModels;

namespace PdfEditor.Views;

public partial class PageView : UserControl
{
    private TextAnnotation? _dragging;
    private Point _dragOffset;

    public PageView()
    {
        InitializeComponent();
    }

    private MainViewModel? GetMainViewModel()
        => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != sender && e.OriginalSource is not Canvas) return;

        if (DataContext is PdfPageModel page)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            GetMainViewModel()?.AddAnnotation(page.PageIndex, pos.X, pos.Y);
        }
        e.Handled = true;
    }

    private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: TextAnnotation ann })
        {
            ann.IsEditing = true;
            e.Handled = true;
        }
    }

    private void Annotation_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TextAnnotation ann } element)
        {
            if (ann.IsEditing) return;
            _dragging = ann;
            _dragOffset = e.GetPosition(element);
            element.CaptureMouse();
            GetMainViewModel()!.SelectedAnnotation = ann;
            e.Handled = true;
        }
    }

    private void Annotation_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null) return;
        var pos = e.GetPosition(AnnotationCanvas);
        _dragging.X = pos.X - _dragOffset.X;
        _dragging.Y = pos.Y - _dragOffset.Y;
    }

    private void Annotation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging is not null && sender is FrameworkElement element)
        {
            element.ReleaseMouseCapture();
            _dragging = null;
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TextAnnotation ann })
            ann.IsEditing = false;
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is FrameworkElement { DataContext: TextAnnotation ann })
        {
            ann.IsEditing = false;
            e.Handled = true;
        }
    }

    private void TextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void Border_Drop(object sender, DragEventArgs e) { }
}
```

**Step 3: Create Converters**

```csharp
// src/PdfEditor/Converters/BoolToVisibilityConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdfEditor.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public class PageNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? (i + 1).ToString() : "?";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

**Step 4: Create MainWindow XAML**

```xml
<!-- src/PdfEditor/MainWindow.xaml -->
<Window x:Class="PdfEditor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:PdfEditor.ViewModels"
        xmlns:views="clr-namespace:PdfEditor.Views"
        xmlns:conv="clr-namespace:PdfEditor.Converters"
        Title="PDF-Editor" Height="800" Width="1100"
        WindowStartupLocation="CenterScreen">
    <Window.DataContext>
        <vm:MainViewModel/>
    </Window.DataContext>
    <Window.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
        <conv:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibility"/>
        <conv:PageNumberConverter x:Key="PageNumberConverter"/>
    </Window.Resources>
    <Window.InputBindings>
        <KeyBinding Key="Delete" Command="{Binding DeleteAnnotationCommand}"/>
    </Window.InputBindings>
    <DockPanel>
        <!-- Toolbar -->
        <ToolBarTray DockPanel.Dock="Top">
            <ToolBar>
                <Button Content="PDF öffnen" Command="{Binding OpenCommand}" Padding="8,4"/>
                <Separator/>
                <Button Content="Speichern" Command="{Binding SaveCommand}" Padding="8,4"/>
                <Button Content="Speichern unter..." Command="{Binding SaveAsCommand}" Padding="8,4"/>
                <Separator/>
                <TextBlock Text="Schriftgröße:" VerticalAlignment="Center" Margin="4,0"/>
                <ComboBox ItemsSource="{Binding FontSizes}"
                          SelectedItem="{Binding SelectedFontSize}"
                          Width="60"/>
                <TextBlock Text="Schriftart:" VerticalAlignment="Center" Margin="8,0,4,0"/>
                <ComboBox ItemsSource="{Binding FontFamilies}"
                          SelectedItem="{Binding SelectedFontFamily}"
                          Width="140"/>
            </ToolBar>
        </ToolBarTray>

        <!-- Status bar -->
        <StatusBar DockPanel.Dock="Bottom">
            <TextBlock Text="{Binding CurrentFilePath, FallbackValue='Keine Datei geöffnet'}"/>
        </StatusBar>

        <!-- PDF pages -->
        <ScrollViewer Background="#F0F0F0" VerticalScrollBarVisibility="Auto"
                      HorizontalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Pages}" Margin="20">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <views:PageView/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </DockPanel>
</Window>
```

**Step 5: Update MainWindow code-behind**

```csharp
// src/PdfEditor/MainWindow.xaml.cs
using System.Windows;

namespace PdfEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

**Step 6: Build and run**

```bash
dotnet build src/PdfEditor
dotnet run --project src/PdfEditor
```
Expected: Application launches, toolbar visible, can open a PDF.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add main UI with toolbar, page view, and text annotation interaction"
```

---

### Task 8: Integration Test — Full Open-Edit-Save Workflow

**Files:**
- Test: `tests/PdfEditor.Tests/Services/IntegrationTests.cs`
- Create: `tests/PdfEditor.Tests/TestData/` (directory for test PDFs)

**Step 1: Write integration test**

```csharp
// tests/PdfEditor.Tests/Services/IntegrationTests.cs
using PdfEditor.Models;
using PdfEditor.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Tests.Services;

public class IntegrationTests
{
    [Fact]
    public void FullWorkflow_CreatePdf_AddAnnotations_SaveAndVerify()
    {
        // Arrange: create a 2-page PDF
        var tempInput = Path.GetTempFileName() + ".pdf";
        var tempOutput = Path.GetTempFileName() + ".pdf";

        using (var doc = new PdfDocument())
        {
            var page1 = doc.AddPage();
            page1.Width = 595;
            page1.Height = 842;
            var page2 = doc.AddPage();
            page2.Width = 595;
            page2.Height = 842;
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
        using var result = PdfReader.Open(tempOutput, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(2, result.PageCount);

        var fileSize = new FileInfo(tempOutput).Length;
        var inputSize = new FileInfo(tempInput).Length;
        Assert.True(fileSize > inputSize, "Output should be larger than input due to added text");

        // Cleanup
        File.Delete(tempInput);
        File.Delete(tempOutput);
    }
}
```

**Step 2: Run all tests**

```bash
dotnet test -v n
```
Expected: All tests pass.

**Step 3: Commit**

```bash
git add -A
git commit -m "test: add integration test for full open-edit-save workflow"
```

---

### Task 9: Polish — App Icon, Window Title Binding, Final Cleanup

**Files:**
- Modify: `src/PdfEditor/MainWindow.xaml` (title shows filename)
- Modify: `src/PdfEditor/App.xaml` (merge resource dictionaries)

**Step 1: Update App.xaml to merge converters globally**

```xml
<!-- src/PdfEditor/App.xaml -->
<Application x:Class="PdfEditor.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:PdfEditor.Converters"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
        <conv:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibility"/>
        <conv:PageNumberConverter x:Key="PageNumberConverter"/>
    </Application.Resources>
</Application>
```

Remove the `Window.Resources` section from `MainWindow.xaml` since converters are now in App.xaml.

**Step 2: Build and run final test**

```bash
dotnet build src/PdfEditor && dotnet test -v n
```
Expected: Build succeeded, all tests pass.

**Step 3: Run the app and test manually**

```bash
dotnet run --project src/PdfEditor
```

Manual test:
1. Click "PDF öffnen" → select a PDF → pages display
2. Click on a page → textbox appears → type text → press Enter
3. Double-click text to edit, drag to move, Delete key to remove
4. Click "Speichern unter..." → save → open saved PDF to verify text is embedded

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: polish app resources and finalize PDF editor v1.0"
```

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace PdfEditor.Models;

public class PdfPageModel : INotifyPropertyChanged
{
    private Bitmap? _renderedImage;

    public int PageIndex { get; }
    public double WidthInPoints { get; set; }
    public double HeightInPoints { get; set; }
    public ObservableCollection<TextAnnotation> Annotations { get; } = new();

    public Bitmap? RenderedImage
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

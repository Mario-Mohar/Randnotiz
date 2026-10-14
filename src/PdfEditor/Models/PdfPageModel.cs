using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace Randnotiz.Models;

public class PdfPageModel : INotifyPropertyChanged
{
    private Bitmap? _renderedImage;

    public int PageIndex { get; }

    /// <summary>Seitengroesse in PDF-Punkten, so wie die Seite angezeigt wird
    /// (/Rotate schon eingerechnet). Unabhaengig vom Zoom.</summary>
    public double WidthInPoints { get; set; }
    public double HeightInPoints { get; set; }

    public ObservableCollection<TextAnnotation> Annotations { get; } = new();

    public Bitmap? RenderedImage
    {
        get => _renderedImage;
        set
        {
            _renderedImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayScale));
        }
    }

    /// <summary>
    /// Anzeigeeinheiten je PDF-Punkt. Die Annotationen liegen in Punkten,
    /// erst die Anzeige rechnet sie mit diesem Faktor auf das Seitenbild um.
    /// Massgeblich ist die Groesse, in der das Bild auf der Flaeche liegt,
    /// nicht seine Pixelzahl.
    /// </summary>
    public double DisplayScale =>
        _renderedImage is null || WidthInPoints <= 0
            ? 1.0
            : _renderedImage.Size.Width / WidthInPoints;

    public PdfPageModel(int pageIndex)
    {
        PageIndex = pageIndex;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

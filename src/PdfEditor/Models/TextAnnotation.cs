using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Randnotiz.Models;

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

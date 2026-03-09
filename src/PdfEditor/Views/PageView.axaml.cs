using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    {
        var window = this.FindAncestorOfType<Window>();
        return window?.DataContext as MainViewModel;
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Visual);
        if (!point.Properties.IsLeftButtonPressed) return;

        // Only handle clicks on the canvas itself, not on child elements
        if (e.Source != sender && e.Source is not Canvas) return;

        if (DataContext is PdfPageModel page)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            GetMainViewModel()?.AddAnnotation(page.PageIndex, pos.X, pos.Y);
        }
        e.Handled = true;
    }

    private void TextBlock_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TextAnnotation ann })
        {
            ann.IsEditing = true;
            e.Handled = true;
        }
    }

    private void Annotation_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Visual);
        if (!point.Properties.IsLeftButtonPressed) return;

        if (sender is Control { DataContext: TextAnnotation ann } element)
        {
            if (ann.IsEditing) return;
            _dragging = ann;
            _dragOffset = e.GetPosition(element);
            e.Pointer.Capture(element);
            GetMainViewModel()!.SelectedAnnotation = ann;
            e.Handled = true;
        }
    }

    private void Annotation_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is null) return;
        var pos = e.GetPosition(AnnotationCanvas);
        _dragging.X = pos.X - _dragOffset.X;
        _dragging.Y = pos.Y - _dragOffset.Y;
    }

    private void Annotation_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is not null)
        {
            e.Pointer.Capture(null);
            _dragging = null;
        }
    }

    private void TextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TextAnnotation ann })
            ann.IsEditing = false;
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is Control { DataContext: TextAnnotation ann })
        {
            ann.IsEditing = false;
            e.Handled = true;
        }
    }

    private void TextBox_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }
}

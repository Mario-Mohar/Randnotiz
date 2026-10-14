using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Randnotiz.Models;
using Randnotiz.ViewModels;

namespace Randnotiz.Views;

public partial class PageView : UserControl
{
    private PdfPageModel? _currentPage;
    private TextAnnotation? _dragging;
    private Point _dragOffset;

    // Tracks the visual control, property-change handler and layout refresh for each annotation.
    private readonly Dictionary<TextAnnotation, (Control Control, PropertyChangedEventHandler Handler, Action Relayout)> _annotationData = [];

    // Annotationen liegen in PDF-Punkten, die Flaeche in Anzeigeeinheiten.
    private double Scale => _currentPage?.DisplayScale ?? 1.0;

    public PageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DataContext wiring
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Detach from previous page
        if (_currentPage is not null)
        {
            _currentPage.Annotations.CollectionChanged -= OnAnnotationsChanged;
            _currentPage.PropertyChanged -= OnPagePropertyChanged;
            foreach (var (ann, (_, handler, _)) in _annotationData)
                ann.PropertyChanged -= handler;
            _annotationData.Clear();
            AnnotationCanvas.Children.Clear();
            _currentPage = null;
        }

        if (DataContext is not PdfPageModel page) return;

        _currentPage = page;
        page.Annotations.CollectionChanged += OnAnnotationsChanged;
        page.PropertyChanged += OnPagePropertyChanged;

        foreach (var annotation in page.Annotations)
            AddAnnotationControl(annotation);
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Neuer Zoom, neues Bild: die Punkte bleiben, nur die Anzeige zieht nach.
        if (e.PropertyName != nameof(PdfPageModel.DisplayScale)) return;
        foreach (var (_, _, relayout) in _annotationData.Values)
            relayout();
    }

    private void OnAnnotationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (TextAnnotation ann in e.NewItems)
                AddAnnotationControl(ann);

        if (e.OldItems is not null)
            foreach (TextAnnotation ann in e.OldItems)
                RemoveAnnotationControl(ann);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Annotation control creation / removal
    // ──────────────────────────────────────────────────────────────────────────

    private void AddAnnotationControl(TextAnnotation annotation)
    {
        // TextBlock shown in display mode
        var textBlock = new TextBlock
        {
            Text = annotation.Text,
            FontFamily = new FontFamily(annotation.FontFamily),
            Foreground = Brushes.Black,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            IsVisible = !annotation.IsEditing,
        };

        // TextBox shown in edit mode
        var textBox = new TextBox
        {
            Text = annotation.Text,
            FontFamily = new FontFamily(annotation.FontFamily),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            CaretBrush = Brushes.Black,
            MinWidth = 50,
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#4A90D9")),
            IsVisible = annotation.IsEditing,
        };

        var grid = new Grid();
        grid.Children.Add(textBlock);
        grid.Children.Add(textBox);

        // Punkte -> Anzeige. Die Schrift waechst mit, damit die Anzeige zeigt,
        // was beim Speichern in der Datei landet.
        void Relayout()
        {
            double scale = Scale;
            Canvas.SetLeft(grid, annotation.X * scale);
            Canvas.SetTop(grid, annotation.Y * scale);
            textBlock.FontSize = annotation.FontSize * scale;
            textBox.FontSize = annotation.FontSize * scale;
        }
        Relayout();

        // TextBox → model (two-way: external model changes are pushed by propHandler below)
        textBox.TextChanged += (_, _) => annotation.Text = textBox.Text ?? "";

        // Double-tap on label enters edit mode
        textBlock.DoubleTapped += (_, e) =>
        {
            annotation.IsEditing = true;
            e.Handled = true;
        };

        // Drag to reposition (display mode only)
        grid.PointerPressed += (s, e) =>
        {
            if (!e.GetCurrentPoint(s as Visual).Properties.IsLeftButtonPressed) return;
            e.Handled = true; // always consume so Canvas doesn't create a new annotation
            if (annotation.IsEditing) return;
            _dragging = annotation;
            _dragOffset = e.GetPosition(grid);
            e.Pointer.Capture(grid);
            GetMainViewModel()?.SelectedAnnotation = annotation;
        };

        grid.PointerMoved += (_, e) =>
        {
            if (_dragging != annotation) return;
            var pos = e.GetPosition(AnnotationCanvas);
            annotation.X = (pos.X - _dragOffset.X) / Scale;
            annotation.Y = (pos.Y - _dragOffset.Y) / Scale;
        };

        grid.PointerReleased += (_, e) =>
        {
            if (_dragging != annotation) return;
            e.Pointer.Capture(null);
            _dragging = null;
        };

        // Finish editing on Escape / Enter or loss of focus; remove if empty
        textBox.LostFocus += (_, _) =>
        {
            annotation.IsEditing = false;
            if (string.IsNullOrEmpty(annotation.Text))
                _currentPage?.Annotations.Remove(annotation);
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Enter)
            {
                annotation.IsEditing = false;
                if (string.IsNullOrEmpty(annotation.Text))
                    _currentPage?.Annotations.Remove(annotation);
                e.Handled = true;
            }
        };

        // React to model property changes
        PropertyChangedEventHandler propHandler = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(TextAnnotation.X):
                case nameof(TextAnnotation.Y):
                case nameof(TextAnnotation.FontSize):
                    Relayout();
                    break;
                case nameof(TextAnnotation.Text):
                    textBlock.Text = annotation.Text;
                    if (textBox.Text != annotation.Text)
                        textBox.Text = annotation.Text;
                    break;
                case nameof(TextAnnotation.FontFamily):
                    var ff = new FontFamily(annotation.FontFamily);
                    textBlock.FontFamily = ff;
                    textBox.FontFamily = ff;
                    break;
                case nameof(TextAnnotation.IsEditing):
                    textBlock.IsVisible = !annotation.IsEditing;
                    textBox.IsVisible = annotation.IsEditing;
                    if (annotation.IsEditing)
                        FocusTextBox(textBox);
                    break;
                case nameof(TextAnnotation.IsSelected):
                    grid.Background = annotation.IsSelected
                        ? new SolidColorBrush(Color.FromArgb(40, 74, 144, 217))
                        : null;
                    break;
            }
        };
        annotation.PropertyChanged += propHandler;

        // If annotation is created in edit mode, focus immediately
        if (annotation.IsEditing)
            FocusTextBox(textBox);

        AnnotationCanvas.Children.Add(grid);
        _annotationData[annotation] = (grid, propHandler, Relayout);
    }

    private void RemoveAnnotationControl(TextAnnotation annotation)
    {
        if (!_annotationData.TryGetValue(annotation, out var entry)) return;
        annotation.PropertyChanged -= entry.Handler;
        AnnotationCanvas.Children.Remove(entry.Control);
        _annotationData.Remove(annotation);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Canvas click → create annotation
    // ──────────────────────────────────────────────────────────────────────────

    private void AnnotationCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed) return;
        if (DataContext is not PdfPageModel page) return;

        var pos = e.GetPosition(AnnotationCanvas);
        double scale = Scale;
        GetMainViewModel()?.AddAnnotation(page.PageIndex, pos.X / scale, pos.Y / scale);
        e.Handled = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static void FocusTextBox(TextBox textBox)
    {
        // Defer until after layout so the control is at its final Canvas position.
        // Without this, BringIntoView scrolls to (0,0) instead of the annotation position.
        Dispatcher.UIThread.Post(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private MainViewModel? GetMainViewModel()
    {
        var window = this.FindAncestorOfType<Window>();
        return window?.DataContext as MainViewModel;
    }
}

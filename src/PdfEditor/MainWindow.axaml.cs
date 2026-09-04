using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Randnotiz.ViewModels;

namespace Randnotiz;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;

        var pdfFile = e.DataTransfer.TryGetFiles()
            ?.FirstOrDefault(f => f.TryGetLocalPath()
                ?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true);

        if (pdfFile is null) return;

        var path = pdfFile.TryGetLocalPath()!;
        if (DataContext is MainViewModel vm)
            await vm.LoadFileAsync(path);
    }
}

# PDF-Editor Design

## Zusammenfassung

Windows Desktop-Anwendung (C# / WPF) zum Bearbeiten von PDF-Dokumenten. Ermöglicht das Ausfüllen von Formularfeldern und das freie Platzieren von Text auf beliebigen Stellen eines PDFs. Mehrseitige Ansicht mit Speichern als neue PDF.

## Technologie-Stack

- **Framework:** .NET 8 / WPF
- **PDF-Bibliothek:** PDFsharp (MIT-Lizenz, kostenlos)
- **PDF-Rendering:** Windows.Data.Pdf API (eingebaut in Windows 10/11)
- **Architektur:** MVVM

## Projektstruktur

```
PdfEditor/
├── PdfEditor.sln
├── PdfEditor/
│   ├── App.xaml
│   ├── MainWindow.xaml              # Hauptfenster mit Toolbar + ScrollViewer
│   ├── Models/
│   │   ├── PdfDocumentModel.cs      # PDF-Dokument-Model (Seiten, Formularfelder)
│   │   └── TextAnnotation.cs        # Platzierter Text (Position, Inhalt, Schriftgröße, Seite)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs         # Hauptlogik (Öffnen, Speichern, Seitenverwaltung)
│   │   └── RelayCommand.cs          # ICommand-Implementierung
│   ├── Views/
│   │   └── PageView.xaml            # Einzelne PDF-Seite mit Canvas-Overlay
│   ├── Services/
│   │   ├── PdfRenderService.cs      # PDF → BitmapImage Rendering via Windows.Data.Pdf
│   │   └── PdfSaveService.cs        # TextAnnotations + Formularfelder ins PDF schreiben
│   └── Converters/
│       └── BoolToVisibilityConverter.cs
```

## Benutzeroberfläche

### Toolbar (oben)
- **PDF öffnen** — OpenFileDialog für .pdf Dateien
- **Speichern** / **Speichern unter** — SaveFileDialog
- **Schriftgröße** — ComboBox (8-72pt)
- **Schriftart** — ComboBox (System-Schriften)

### Hauptbereich
- ScrollViewer mit allen PDF-Seiten vertikal untereinander
- Jede Seite besteht aus:
  - Gerendertes PDF-Bild (Hintergrund)
  - Transparenter WPF Canvas (Overlay für Text-Platzierung)
- Seitennummer unter jeder Seite

### Interaktion
- **Klick auf freie Stelle** → Neue Textbox erscheint an Mausposition → Text eingeben
- **Formularfelder** → Automatisch als editierbare Textboxen über den Feld-Positionen
- **Drag & Drop** → Platzierte Texte können verschoben werden
- **Doppelklick** → Text bearbeiten
- **Delete-Taste** → Ausgewählten Text löschen

## Datenfluss

### PDF öffnen
1. Benutzer wählt PDF über OpenFileDialog
2. `PdfRenderService` rendert jede Seite als BitmapImage via Windows.Data.Pdf API
3. `PdfDocumentModel` liest Formularfelder (AcroForm) via PDFsharp aus
4. UI zeigt Seitenbilder + Formularfeld-Overlays an

### Text bearbeiten
1. Klick auf Canvas → neue `TextAnnotation` wird erstellt (X, Y, Seitennummer)
2. Textbox erscheint → Benutzer tippt Text
3. TextAnnotation wird in der Annotations-Liste des ViewModels gespeichert

### PDF speichern
1. PDFsharp öffnet das Original-PDF
2. Für jede TextAnnotation: `XGraphics.DrawString()` an der korrekten Position
3. Formularfelder werden über AcroForm API gefüllt
4. PDF wird gespeichert (neuer Pfad oder überschreiben)

## PDF-Rendering

PDFsharp kann PDFs nicht als Bilder rendern. Lösung:

```csharp
// Windows.Data.Pdf API (UWP, verfügbar via WinRT in .NET 8)
var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(storageFile);
var page = pdfDoc.GetPage(pageIndex);
await page.RenderToStreamAsync(stream);
// Stream → BitmapImage für WPF
```

## Fehlerbehandlung

- **Ungültige PDF:** MessageBox mit Fehlermeldung beim Öffnen
- **Schreibgeschützte PDF:** Warnung, "Speichern unter" erzwingen
- **Große PDFs:** Seiten werden lazy gerendert (nur sichtbare Seiten)

## Nicht im Scope (YAGNI)

- Bilder/Stempel einfügen
- Unterschriften
- PDF-Seiten löschen/umordnen
- OCR / Texterkennung
- Mehrbenutzerbetrieb

# Randnotiz

Ein schlanker, plattformübergreifender PDF-Editor zum Öffnen und Kommentieren von PDF-Dokumenten.

## Features

- PDF-Dateien öffnen und anzeigen
- Text-Annotationen auf beliebigen Stellen hinzufügen
- Schriftart und Schriftgröße frei wählbar
- Änderungen direkt in die PDF-Datei speichern
- Läuft nativ auf **Windows** und **Linux** (inkl. Arch-basierte Systeme wie CachyOS)

## Download

Die neueste Version steht unter [Releases](https://github.com/Mario-Mohar/Randnotiz/releases/latest) bereit.

| Plattform | Datei |
|---|---|
| Windows | `PdfEditor-win-x64.zip` |
| Linux | `PdfEditor-linux-x64.zip` |

Einfach entpacken und starten — keine Installation notwendig.

## Verwendung

1. **PDF öffnen** — Schaltfläche "PDF öffnen" klicken und eine Datei auswählen
2. **Annotation hinzufügen** — Auf eine beliebige Stelle im Dokument klicken
3. **Text eingeben** — Schriftart und -größe in der Toolbar anpassen
4. **Speichern** — "Speichern" oder "Speichern unter..." verwenden

## Entwicklung

**Voraussetzungen:** .NET 10 SDK

```bash
git clone https://github.com/Mario-Mohar/Randnotiz.git
cd Randnotiz
dotnet run --project src/PdfEditor
```

Tests ausführen:

```bash
dotnet test
```

## Lizenz

Dieses Projekt steht unter der [GNU General Public License v3.0](LICENSE).

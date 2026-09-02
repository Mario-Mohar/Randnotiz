# Randnotiz

[![codecov](https://codecov.io/gh/Mario-Mohar/Randnotiz/graph/badge.svg)](https://codecov.io/gh/Mario-Mohar/Randnotiz)

Ein schlanker, plattformübergreifender PDF-Editor zum Öffnen und Kommentieren von PDF-Dokumenten.

## Features

- PDF-Dateien öffnen und anzeigen (auch per Drag & Drop)
- Text-Annotationen auf beliebigen Stellen hinzufügen, verschieben und löschen
- Schriftart und Schriftgröße frei wählbar
- Zoom von 50% bis 200%
- Änderungen direkt in die PDF-Datei speichern
- Läuft nativ auf **Windows** und **Linux** (inkl. Arch-basierte Systeme wie CachyOS)

## Download

Die neueste Version steht unter [Releases](https://github.com/Mario-Mohar/Randnotiz/releases/latest) bereit.

| Plattform | Datei |
|---|---|
| Windows | `Randnotiz-win-x64.zip` |
| Linux | `Randnotiz-linux-x64.zip` |

Einfach entpacken und starten — keine Installation notwendig.

## Verwendung

1. **PDF öffnen** — Schaltfläche "PDF öffnen" klicken oder PDF-Datei auf das Fenster ziehen
2. **Annotation hinzufügen** — Auf eine beliebige Stelle im Dokument klicken
3. **Text eingeben** — Enter oder Klick außerhalb beendet die Bearbeitung
4. **Annotation verschieben** — Im Anzeigemodus (nach Bearbeitung) auf die Annotation klicken und ziehen
5. **Annotation löschen** — Annotation anklicken, dann Entf drücken
6. **Zoom** — Mit den +/−-Schaltflächen in der Toolbar zoomen
7. **Speichern** — "Speichern" oder "Speichern unter..." verwenden

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

## Mitarbeit

Fehlerberichte, Funktionswünsche und Pull Requests sind willkommen — etwas zu
finden, das nicht stimmt, und es aufzuschreiben ist ein echter Beitrag, und der
nützlichste dazu.

Die Einzelheiten stehen in **[CONTRIBUTING.md](CONTRIBUTING.md)**: was eine
Meldung brauchbar macht, wie eine Korrektur über einen Fork zu dir kommt, und
was nach dem Absenden passiert.

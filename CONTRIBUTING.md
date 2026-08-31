# Mitarbeit

Danke fürs Vorbeischauen. Das Projekt ist klein, der Ablauf entsprechend kurz.
Oberfläche und Dokumentation sind auf Deutsch; englische Beiträge sind
willkommen, dann übersetze ich die nutzersichtbaren Texte beim Zusammenführen.

## Einrichten

Es braucht das **.NET-10-SDK**. Auf vielen Linux-Systemen liegt nur die
Laufzeitumgebung im Paketmanager, nicht das SDK — dann ohne Rootrechte holen:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet --no-path
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
```

Danach:

```bash
git clone https://github.com/Mario-Mohar/Randnotiz.git
cd Randnotiz
dotnet run --project src/PdfEditor
```

**Der Standardzweig heißt `master`, nicht `main`.** `git push origin main`
scheitert hier.

## Prüfungen

Die Pipeline führt genau das aus, was du hier ausführen kannst:

```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

`-warnaserror` steht bewusst nur in der Pipeline und nicht in der `csproj`: eine
neue Warnung soll einen Pull Request rot machen, aber niemandem den lokalen
Build zerlegen, der gerade an einer halbfertigen Änderung arbeitet.

## Was beim Ändern wichtig ist

**Rendern läuft serialisiert, und es muss abbrechbar bleiben.** Mehrere
gleichzeitige Renderaufträge auf dasselbe Dokument haben sich in die Quere
gekommen, darum liegt eine Sperre davor. Wer daran etwas ändert: die
Skalierung der Annotationen gehört **vor** die Renderschleife, nicht hinein.

Dazu eine Warnung aus der Erfahrung: ein Test, der eine Sperre selbst hält und
dann `WaitAsync()` ohne Token aufruft, **hängt** statt fehlzuschlagen. Solche
Tests brauchen eine Frist — `Task.WhenAny` mit einem `Task.Delay` daneben —
sonst steht der Testlauf still, statt rot zu werden.

**Speichern darf die Ausgangsdatei nie beschädigen.** Wer am Speicherpfad
arbeitet, schreibt erst vollständig woandershin und ersetzt dann.

**`Tmds.DBus.Protocol` ist absichtlich festgenagelt.** Avalonia.FreeDesktop
zieht transitiv 0.20.0 herein, das eine bekannte Schwachstelle hat
(GHSA-xrw6-gwf8-vvr9). Der direkte Verweis auf 0.21.3 hebt die Auflösung an.
**Ein Avalonia-Update löst das nicht** — auch 11.3.7 hängt noch an 0.21.2, der
Fix ist erst 0.21.3; frühestens Avalonia 12 zieht genug nach. Die
Ausstiegsbedingung steht als Kommentar in der `csproj`. Bitte weder entfernen
noch stillschweigend anheben.

Eine Kleinigkeit, die dabei Zeit kostet: **XML-Kommentare vertragen kein `--`.**
Zwei Bindestriche in einer `csproj`-Anmerkung brechen den Build mit MSB4025.

## Pull Requests

- Zweig von `master` weg. Der Zweigname ist frei.
- Commit-Stil `fix(bereich):`, `feat(bereich):`, `docs:`, `chore:`. Die
  Pipeline liest das Präfix des PR-Titels für die Beschriftung.
- Die Pipeline kommentiert das Ergebnis und aktualisiert diesen Kommentar bei
  jedem Push. Grün und kein Entwurf ergibt die Marke `ready-to-merge`.
- Für einen genaueren Blick können Betreuer `/claude review` kommentieren.

`build.yml` erzeugt weiterhin die Linux- und Windows-Pakete. Die Pipeline hier
ist bewusst die schnelle Hälfte, damit sie an einem Pull Request nützlich ist.

## Etwas melden

Bitte die Issue-Vorlagen benutzen. Bei einem Fehler beim Öffnen oder Speichern
hilft fast immer die Art des Dokuments — verschlüsselt, formularbasiert,
gescannt, sehr groß, ungewöhnliche Schriften. **Bitte keine PDF mit privatem
Inhalt anhängen.**

## Lizenz

MIT, wie das Projekt. Mit deinem Beitrag stimmst du zu, dass er darunter
erscheint.

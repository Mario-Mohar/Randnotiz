# Mitarbeit

Oberfläche und Dokumentation sind auf Deutsch; englische Beiträge sind
willkommen, dann übersetze ich die nutzersichtbaren Texte beim Zusammenführen.

## Beiträge sind erwünscht

Das hier ist ein kleines Projekt, das eine einzelne Person nebenher pflegt — und
genau deshalb ist ein fremder Blick viel wert. **Einen Fehler zu finden und
aufzuschreiben ist ein echter Beitrag**, vermutlich sogar der nützlichste: ich
benutze das nur auf meinem eigenen Rechner, mit meiner eigenen Einrichtung, und
das meiste, was kaputt ist, ist dort kaputt, wo ich nie hinschaue.

Drei Wege zu helfen, sortiert danach, was sie dich kosten:

### 1. Etwas melden, das nicht stimmt

Ein Issue mit der Vorlage **Fehlerbericht** aufmachen. Sie fragt das ab, was ich
sonst nachfragen müsste — und eine Nachfrage kostet uns beide einen Tag.

Worauf es wirklich ankommt:

- **Was du erwartet hast, und was stattdessen passiert ist.** Beide Hälften.
  „Geht nicht" ist die eine Meldung, mit der ich nichts anfangen kann.
- **Die Schritte dorthin.** Wenn du es wiederholen kannst, schreib wie. Wenn es
  nur einmal auftrat, schreib auch das — ein sporadischer Fehler ist trotzdem
  wissenswert, und „ich konnte es nicht nachstellen" ist eine Information, kein
  Ausschlussgrund.
- **Deine Umgebung**, so wie die Vorlage danach fragt.

Feil nicht daran herum. Eine grobe Meldung heute ist mehr wert als eine perfekte,
die nie geschrieben wird. Und wenn du unsicher bist, ob etwas überhaupt ein
Fehler ist: mach es trotzdem auf. Das zu entscheiden ist meine Aufgabe, nicht
deine.

### 2. Vorschlagen, was es können sollte

Ein Issue mit der Vorlage **Funktionswunsch**.

Sie fragt zuerst, was du *erreichen* willst, und erst dann, was gebaut werden
soll. Das ist Absicht und keine Hürde: ungefähr in der Hälfte der Fälle gibt es
einen einfacheren Weg als den, den wir beide zuerst im Kopf hatten — aber der
zeigt sich nur, wenn ich die Ausgangslage kenne.

Ein abgelehnter Wunsch ist kein vergeudetes Issue. „Jetzt nicht" und „nicht in
diesem Projekt" bekommst du schnell und mit Begründung.

### 3. Eine Korrektur oder Funktion schicken

Sehr willkommen, und für Kleinigkeiten musst du nicht vorher fragen.

**Bei allem, was über ein paar Zeilen hinausgeht, vorher ein Issue aufmachen** —
oder am bestehenden kommentieren — und sagen, dass du daran arbeitest. Das kostet
dich einen Satz und erspart dir den Fall, dass ich dasselbe am selben Abend
behoben habe oder es anders gelöst haben wollte.

Weil du in dieses Repository nicht schreiben kannst, läuft es über einen Fork:

```bash
# 1. Auf GitHub forken, dann deinen Fork klonen
git clone https://github.com/<dein-benutzername>/Randnotiz.git
cd Randnotiz

# 2. Ein Zweig. Name egal.
git switch -c fix/die-sache

# 3. Ändern, worum es dir geht, dann die Prüfungen unten laufen lassen

# 4. In deinen Fork pushen und den Pull Request aufmachen
git push -u origin fix/die-sache
```

GitHub bietet dir danach den Knopf für den Pull Request an. Fülle die Vorlage
aus, und wenn er ein Issue erledigt, schreib `Fixes #12` hinein — dann schließt
es sich beim Zusammenführen von selbst.

## Was danach passiert

1. **Die Pipeline läuft** und schreibt einen Kommentar an deinen Pull Request,
   mit einer Tabelle, was durchgelaufen ist. Sie aktualisiert denselben
   Kommentar bei jedem Push, es gibt also eine Stelle zum Nachsehen statt eines
   wachsenden Stapels.
2. **Sie beschriftet den Pull Request** nach Umfang und Art und setzt
   `ready-to-merge`, sobald alles grün ist.
3. **Beim allerersten Beitrag warten die Prüfungen auf meine Freigabe.** Das
   macht GitHub von sich aus, damit fremder Code die Rechenzeit nicht ungefragt
   benutzt. Wenn dein Pull Request bei „waiting for approval" steht, **ist nichts
   kaputt und du musst nichts tun** — ich muss einmal klicken.
4. **Zusammengeführt wird von mir.** Auf den Standardzweig kommt nichts, was
   nicht durch einen Pull Request mit grünen Prüfungen gegangen ist; das gilt
   auch für meine eigenen Commits.

Ist eine Prüfung rot, steht im Protokoll welche und warum. Frag im Pull Request
nach, wenn es nicht offensichtlich ist — eine rote Pipeline ist keine Absage,
und ziemlich oft liegt der Fehler bei ihr und nicht bei dir.

Ich mache das neben einem Beruf, eine Antwort kann also ein paar Tage dauern.
Das ist kein Desinteresse.

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

**`Tmds.DBus.Protocol` war bis Avalonia 12 festgenagelt.** Avalonia.FreeDesktop
zog transitiv 0.20.0 herein, das eine bekannte Schwachstelle hat
(GHSA-xrw6-gwf8-vvr9), und ein direkter Verweis auf 0.21.3 hob die Auflösung an.
Mit Avalonia 12.1.1 löst die Kette von selbst 0.94.1 auf, der Pin ist damit
weg. Falls du an den Abhängigkeiten arbeitest: prüfe solche Fälle mit
`dotnet nuget why src/PdfEditor/PdfEditor.csproj <Paket>`, und wenn du einen
Pin setzt, schreib die Ausstiegsbedingung als Kommentar daneben. Ohne die weiß
später niemand, wann er wieder weg darf.

Eine Kleinigkeit, die dabei Zeit kostet: **XML-Kommentare vertragen kein `--`.**
Zwei Bindestriche in einer `csproj`-Anmerkung brechen den Build mit MSB4025.

## Pull Requests

- Zweig von `master` weg, **in deinem Fork** (siehe oben). Der Zweigname ist frei.
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

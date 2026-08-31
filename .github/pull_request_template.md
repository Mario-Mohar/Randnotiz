## Was sich ändert

<!-- Was macht der Pull Request, und warum? -->

## Art der Änderung

- [ ] Fehlerbehebung
- [ ] Neue Funktion
- [ ] Verhaltensänderung, die nicht abwärtskompatibel ist
- [ ] Umbau, Tests oder Werkzeuge
- [ ] Dokumentation

## Zugehörige Issues

<!-- "Fixes #12". Leer lassen, wenn es keine gibt. -->

## Screenshots

<!-- Bei allem, was in der Oberfläche sichtbar ist. -->

## Checkliste

- [ ] `dotnet format --verify-no-changes`, `dotnet build -warnaserror` und `dotnet test` laufen durch
- [ ] Speichern kann die Ausgangsdatei unter keinen Umständen beschädigen
- [ ] Tests, die selbst eine Sperre halten, haben eine Frist und können nicht hängen
- [ ] Der Pin auf `Tmds.DBus.Protocol` ist unverändert, oder die Anhebung ist oben begründet

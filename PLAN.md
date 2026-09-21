# PLAN: Werte-Ranking (Blazor WebAssembly)

Arbeitstitel: **ValueRanker** (Platzhalter, jederzeit umbenennbar)

## 1. Ziel

Eine rein clientseitige Web-App (Blazor WebAssembly, .NET 10, gehostet auf GitHub Pages), mit der eine Person aus ca. 250 persönlichen Arbeitswerten ihre **Top-3** (und eine verlässliche **Top-10**) ermittelt. Der Benutzer bewertet zuerst kleine Gruppen von Werten, am Ende entscheiden Duelle über die Spitze. Ein Algorithmus wählt Gruppen und Paarungen so, dass in ca. 15 Minuten ein brauchbares Ergebnis entsteht.

Zusätzlich steht eine kürzere, kompakte Werteliste (38 Werte pro Sprache) zur Auswahl, bei der inhaltlich sehr ähnliche Werte zu je einem Wert zusammengelegt wurden. Sie ist die neue Standardauswahl bei der Laufserstellung und verkürzt den Durchlauf spürbar; die vollständige Liste bleibt wählbar.

Nicht-Ziele: Backend, Benutzerkonten, vollständige Sortierung aller 250 Werte, Export (kommt in Iteration 2).

## 2. Rahmenbedingungen

- .NET 10 (LTS), Blazor WebAssembly (standalone), kein Server.
- UI-Bibliothek: **MudBlazor** (Vorschlag). Meilenstein 1 prüft die Kompatibilität mit .NET 10; falls Probleme: Fluent UI Blazor als Alternative.
- Code, Kommentare, Commit-Messages, Bezeichner: **Englisch**.
- UI: **Deutsch und Englisch**, Umschaltung zur Laufzeit, Wahl wird gespeichert.
- Persistenz: nur `localStorage` des Browsers (via JS-Interop hinter einem Interface).
- Hosting: GitHub Pages (Base-Href, SPA-Fallback, GitHub-Actions-Deployment).
- Review: Nach jedem Meilenstein stoppt Claude Code und wartet auf Freigabe. Ein Meilenstein = ein oder mehrere saubere Commits.

## 3. Projektstruktur (Vorschlag)

```
/src
  ValueRanker.Core        # Class library (net10.0): domain, application use cases, ports. No UI/browser dependencies
    Domain/               # Value, Run, events, Elo, ranking strategy (pure logic)
    Application/          # RankingService (driving port) and use cases
    Ports/                # IRunRepository, IValueListProvider, IClock (driven ports)
  ValueRanker.Web         # Blazor WASM app: pages, components, localization
    Adapters/             # LocalStorageRunRepository, HttpValueListProvider (driven adapters)
/tests
  ValueRanker.Core.Tests  # TUnit tests for Core only
/.github/workflows        # Build, test, deploy to GitHub Pages
PLAN.md
CLAUDE.md
```

Bewusst nur **zwei Produktivprojekte**: Die Schichten in `Core` sind Ordner und Namespaces, keine eigenen Projekte. Bei Bedarf (z. B. für eine API) lässt sich das später mechanisch aufteilen.

### Architektur (Ports und Adapters, pragmatisch)

```
 Blazor UI  ──►  RankingService  ──►  Domain (events, Elo, strategy)
 (or API)        (driving port)             │
                       │                    ▼
                       └──►  IRunRepository, IValueListProvider, IClock
                              (driven ports)   ▲
                                               │ implemented by
                              LocalStorageRunRepository, HttpValueListProvider  (adapters in Web)
```

- **Driving port:** `RankingService` in `Core.Application` ist der einzige Einstiegspunkt für die UI. Er bietet Use Cases wie `CreateRun`, `ListRuns`, `DeleteRun`, `GetNextStep`, `SubmitGroup`, `SubmitDuel`, `Undo`, `GetResult`. Die UI kennt weder Elo noch Events, nur diese Methoden und deren Ergebnistypen (Records).
- **Driven ports:** `IRunRepository` (Läufe speichern/laden), `IValueListProvider` (Wertelisten laden), `IClock` (Zeit, für testbare Zeitstempel). Alle asynchron.
- **Adapter in `Web`:** localStorage-Repository (JS-Interop) und HTTP-Provider für die JSON-Listen aus `wwwroot`. Blazor-Komponenten hängen ausschliesslich von `RankingService` ab.
- **Austauschbarkeit:** Eine spätere API kann `Core` unverändert einbinden und liefert nur eigene Adapter (z. B. Datenbank-Repository). Eine mobile App spricht dann die API. Dafür ist nichts weiter vorbereitet als saubere Grenzen: Core hat keine Abhängigkeit auf Blazor, Browser oder JS-Interop, und die Ein- und Ausgabetypen der Use Cases sind einfache, serialisierbare Records.
- **Bewusst weggelassen** (Overengineering für diese Grösse): eigenes DTO-Mapping-Layer, MediatR/CQRS, DI-Container-Frameworks über den .NET-Standard hinaus, Repository-Generics.

### Tests

- Automatisierte Tests **nur für `Core`** (Fachlogik und Use Cases), mit **TUnit**. Keine UI-Tests (kein bUnit, kein Playwright) in dieser Iteration.
- Use Cases werden gegen **In-Memory-Fakes** der Ports getestet (`InMemoryRunRepository`, `FakeValueListProvider`, `FakeClock`), Adapter in `Web` werden nicht automatisiert getestet.
- Ranking-Verhalten ist über den `seed` deterministisch und damit exakt reproduzierbar testbar.

## 4. Datenmodell

**Value list** (`wwwroot/data/values.de.json`, `values.en.json`; je Sprache eine eigene Liste, weil Begriffe nicht 1:1 übersetzbar sind):

```json
{
  "listId": "de-default",
  "language": "de",
  "version": 1,
  "title": "Persönliche Arbeitswerte",
  "values": [
    { "id": "de-001", "name": "Verlässlichkeit", "description": "Zusagen einhalten und Kolleginnen und Kollegen sich auf mich verlassen lassen." }
  ]
}
```

- `id` ist innerhalb einer Liste stabil und eindeutig.
- Die Beschreibung (1-2 Sätze) bezieht sich auf den Business-Kontext.

**Run** (in localStorage gespeichert):

- `id` (GUID), `name`, `createdAt`, `updatedAt`
- `listId` (welche Werteliste), `listVersion`
- `seed` (für deterministische Gruppen und Paarungen)
- `phase` (Build / Focus / Finale / Finished)
- `events`: geordnete Liste aller Entscheidungen (siehe unten)
- `schemaVersion` (für spätere Migrationen)

**Event-Typen:**

- `GroupBestWorst`: Gruppe (Liste von Wert-IDs), gewählter Bester, gewählter Schlechtester
- `GroupFullOrder`: Gruppe, vollständige Reihenfolge
- `Duel`: zwei Wert-IDs, Ergebnis `Left`, `Right` oder `Equal`

**Event-Sourcing-Ansatz:** Der Zustand (Ratings, Phase, nächste Gruppe oder Paarung) wird immer aus `seed` + `events` per Replay berechnet. Vorteile: "Rückgängig" = letztes Event entfernen; Verhalten ist deterministisch und gut testbar; Export in Iteration 2 ist trivial.

## 5. Ranking-Algorithmus

Grundidee: Jedes Event liefert Paarvergleiche, die in ein gemeinsames **Elo-Rating** einfliessen (Startwert 1000, K-Faktor konfigurierbar, `Equal` zählt als 0.5). Eine Gruppe mit 4 Werten und Best/Worst liefert 5 von 6 Paarvergleichen, eine vollständige Ordnung liefert alle 6. Zusätzlich wird pro Wert eine **Unsicherheit** geführt (vereinfacht: Anzahl bisheriger Vergleiche), damit die Auswahl der nächsten Gruppen gezielt bleibt.

Alle Zahlen unten sind **Startwerte für die Simulation in M3** und keine Vorgaben. Sie stehen als Konstanten in `RankingOptions`.

### Phase Build (ca. 8-9 Minuten)

- Gruppen à **4 Werte**, Aufgabe: "Wähle den wichtigsten und den unwichtigsten Wert" (2 Taps, Best/Worst).
- Durchgang 1: Alle 250 Werte kommen genau einmal vor (ca. 63 Gruppen, per Seed zufällig gebildet).
- Durchgang 2: Gruppen aus den oberen ca. 120 Werten nach Rating. Werte mit hoher Unsicherheit (z. B. solche, die in Durchgang 1 in der Mitte landeten) werden bevorzugt, damit starke Werte, die in einer starken Gruppe nur Zweiter oder Dritter wurden, nicht verloren gehen (ca. 30 Gruppen).
- Übergang zu Focus, sobald sich eine Spitzengruppe von ca. 30 Werten abzeichnet.

### Phase Focus (ca. 3 Minuten)

- Gruppen à 4 aus den oberen ca. 30 Werten, Aufgabe: **vollständige Reihenfolge** (3 Taps, der vierte Wert ergibt sich).
- Gruppen werden aus Werten mit ähnlichem Rating gebildet.
- Ziel: Die Top-12 sind eingegrenzt und haben mindestens 3 bis 4 Vergleiche.

### Phase Finale: Duelle (ca. 3 Minuten)

- Betrachtet die aktuellen Top-12 (Konstante). **Keine Ranking-Liste per Drag and Drop**, sondern nur Duelle.
- Zwei grosse Kacheln nebeneinander, Buttons "Beide gleich wichtig" und "Rückgängig".
- Paarung: Werte mit benachbarten Rängen und kleinem Rating-Abstand, bevorzugt Paarungen, die noch nicht stattfanden. Besonders die Ränge 1-5 werden dichter verglichen als der Rest.
- Ein vollständiges Round-Robin (bei 10 Werten 45 Duelle) ist zu viel. Erwartet werden ca. 20-30 adaptive Duelle.
- Abbruchkriterium: Top-3 sind über die letzten N Duelle (Startwert 10) in Menge und Reihenfolge unverändert, und jeder Top-10-Wert hat mindestens 3 Duelle. Zusätzlich harte Obergrenze.
- Der Benutzer kann jederzeit "Ergebnis jetzt ansehen" wählen und danach weitermachen.

### Abstraktion

```csharp
public interface IRankingStrategy
{
    RankingState Apply(RankingState state, RankingEvent evt);
    NextStep GetNextStep(RankingState state); // next group, next duel, or finished
    bool IsFinished(RankingState state);
}
```

Die Elo-Formel selbst liegt in einer separaten, rein mathematischen Klasse (`EloCalculator`). Die Konvertierung von Gruppenevents in Paarvergleiche liegt in einer eigenen, getesteten Klasse.

### Simulation (Teil von M3)

Ein virtueller Benutzer mit bekannter Zielreihenfolge und einstellbarem Rauschen (mehr Rauschen bei benachbarten Rängen) durchläuft das Verfahren mit verschiedenen Seeds. Gemessen werden: Anzahl Taps insgesamt, Trefferquote Top-3 (Menge und Reihenfolge), Trefferquote Top-10 (Menge). Damit werden Gruppengrösse (4 oder 5), Format (Best/Worst oder vollständig), Übergangszeitpunkte und Konstanten mit Zahlen entschieden.

## 6. Benutzerführung

1. **Startseite:** Liste bestehender Läufe (Name, Datum, Fortschritt) mit "Fortsetzen", "Umbenennen", "Löschen". Button "Neuer Lauf": Name (Vorschlag: Datum), Werteliste wählen (Deutsch/Englisch).
2. **Gruppenseite** (Build und Focus): 4 Kacheln mit Wertname (frühe Phase: Name plus einzeilige Kurzbeschreibung, Focus: volle Beschreibung). Bedienung primär per **Antippen**: In Build zuerst den wichtigsten, dann den unwichtigsten Wert; in Focus die Werte der Reihe nach. Ein Tap auf einen gewählten Wert macht die Auswahl rückgängig. Schaltfläche "Rückgängig" für das letzte abgeschlossene Event.
3. **Duell-Seite** (Finale): zwei grosse Kacheln nebeneinander (auf schmalen Bildschirmen untereinander) mit Wertname und Beschreibung. Darunter: "Beide gleich wichtig", "Rückgängig", "Ergebnis ansehen".
4. **Fortschrittsanzeige:** Ehrlich als Schätzung gekennzeichnet (Phase und ungefähre Anzahl verbleibender Schritte).
5. **Ergebnisseite:** Werte nach Rating sortiert. Top-3 hervorgehoben (z. B. grosse Karten mit Rang), Ränge 4-10 normal betont, Rest in kompakter Liste. Zeigt auch, wie belastbar das Ergebnis ist (Anzahl Vergleiche pro Wert).
6. **Sprachumschalter** in der App-Bar (DE/EN).

Barrierefreiheit: Tastatursteuerung (Zahlentasten 1-4 für Gruppen, Pfeil links/rechts und Gleichstand bei Duellen, Rückgängig), Fokus-Indikatoren, ausreichender Kontrast.

Drag and Drop ist kein Bestandteil des Grundumfangs (auf Touch-Geräten in Blazor unzuverlässig). Optional für Iteration 2.

## 7. Meilensteine

Nach jedem Meilenstein: Zusammenfassung der Änderungen, Testergebnis, offene Fragen, dann **Stopp bis zur Freigabe**.

**M0: Vorbereitung.** Repository-Struktur, `.gitignore`, `.editorconfig`, `CLAUDE.md` und `PLAN.md` liegen im Root. Keine Codeänderung.
Abnahme: Struktur wie in Abschnitt 3 angelegt.

**M1: Projektgerüst und Deployment.** Solution mit `Core`, `Web`, `Core.Tests` (TUnit, ein Beispieltest; Testlauf lokal und in CI verifizieren, inkl. der für .NET 10 nötigen Konfiguration des Test-Runners). Blazor WASM mit MudBlazor (Kompatibilität mit .NET 10 prüfen und dokumentieren), leere Startseite. GitHub-Actions-Workflow: Build, Test, Publish nach GitHub Pages inkl. `<base href>`-Anpassung, `404.html`-Fallback, `.nojekyll`.
Abnahme: Seite ist unter der GitHub-Pages-URL erreichbar, Routing funktioniert auch bei direktem Aufruf einer Unterseite, CI ist grün.

**M2: Datenmodell und Werte-Listen.** Modelle in `Core`, JSON-Loader, Validierung (eindeutige IDs, keine leeren Texte). Claude konvertiert deine Liste (`values.de.txt`) in JSON und **entwirft die Beschreibungen in Batches zu je 50 Werten**, die du jeweils reviewst. Englische Liste analog als eigenständige Liste (keine Wort-für-Wort-Übersetzung).
Abnahme: Beide JSON-Dateien laden fehlerfrei, Validierungstests sind grün, du hast alle Beschreibungen freigegeben.

**M3: Fachlogik, Use Cases und Simulation.** `EloCalculator`, Umwandlung von Gruppenevents in Paarvergleiche, `IRankingStrategy` mit den drei Phasen, Replay aus Events, Undo. Danach `RankingService` mit den Use Cases und den Ports samt In-Memory-Fakes (Abschnitt 3). Tests mit TUnit. Simulation wie in Abschnitt 5, ausgeführt über den `RankingService`.
Abnahme: Alle Tests grün. Simulationsergebnis als Tabelle im Commit-Text (Anzahl Taps, Trefferquote Top-3/Top-10 über mehrere Seeds und Rauschstufen). Ziel: Top-3 bei 250 Werten in ca. 150-250 Taps zuverlässig gefunden. Wenn das Ziel verfehlt wird, schlägt Claude Anpassungen vor, ändert aber die Strategie nicht eigenmächtig.

**M4: Adapter in `Web`.** `LocalStorageRunRepository` (JS-Interop) und `HttpValueListProvider` als Implementierungen der Ports aus M3, DI-Registrierung, Schema-Version, Fehlerbehandlung (Speicher voll, Daten korrupt, Storage blockiert). Kein automatisierter Test der Adapter, manuelle Prüfung im Browser.
Abnahme: Lauf anlegen, Seite neu laden, Lauf fortsetzen funktioniert; mehrere Läufe parallel.

**M5: UI.** Startseite, Gruppenseite, Duell-Seite, Ergebnisseite, wie in Abschnitt 6. Zunächst mit hartcodierten deutschen Texten.
Abnahme: Kompletter Durchlauf im Browser möglich, Undo und Gleichstand funktionieren, Fortsetzen nach Reload klappt.

**M6: Lokalisierung.** UI-Texte auf `IStringLocalizer` (resx) umstellen, Deutsch und Englisch, Sprachumschalter, Auswahl wird gespeichert. Werteliste folgt der Auswahl beim Anlegen eines Laufs (ein Lauf bleibt bei seiner Liste).
Abnahme: Keine hartcodierten Texte mehr in Komponenten, beide Sprachen vollständig.

**M7: Politur.** Ergebnisseite hervorheben, Responsive Layout, Tastaturbedienung, Barrierefreiheit, Fehlerseiten, kurze README (Zweck, Lokal starten, Deployment).
Abnahme: Manueller Durchlauf auf Desktop und Smartphone, reale Dauer wird gemessen und mit dem Zeitbudget verglichen.

**Iteration 2 (nicht Teil des Auftrags):** Export (JSON/CSV/Druckansicht), Import, Drag and Drop als Alternative zum Antippen, weitere Listen.

## 8. Arbeitsweise für Claude Code

- Nur einen Meilenstein pro Runde bearbeiten. Nicht vorausarbeiten.
- Vor jedem Meilenstein kurz den Plan für diesen Schritt nennen; bei Unsicherheit fragen statt raten.
- Kleine, thematisch saubere Commits mit englischen Messages.
- Neue NuGet-Pakete nur mit Begründung; Standardbibliothek und .NET-Bordmittel bevorzugen.
- Keine Änderungen an der Ranking-Strategie (Abschnitt 5) ohne Rücksprache.
- Architektur-Grenzen (Abschnitt 3) einhalten: UI nur über `RankingService`, keine Browser- oder Blazor-Abhängigkeit in `Core`. Bei Konflikten nachfragen statt die Grenze aufzuweichen.

## 9. Offene Punkte

- [x] Gruppengrösse: Startwert 4, in M3 anhand der Simulation (N=250) auf 5 erhöht. Nach Einführung der kompakten Werteliste (zunächst ~50-60 Werte) erneut simuliert: 4 verbessert die kompakte Liste deutlich (z. B. Top-3-Trefferquote 76%→92% bei Rauschen 0), verschlechtert aber die vollständige Liste (80%→64%, +18% Taps). Bewusst als globale Konstante wieder auf 4 gesetzt (Kompromiss zugunsten der kompakten Liste), siehe Commit "Reduce GroupSize from 5 to 4...". Die kompakte Liste wurde danach auf 38 Werte weiter verdichtet (siehe `RankingSimulationCompactTests`, N=38).
- [ ] MudBlazor vs. Fluent UI Blazor (Vorschlag: MudBlazor, Prüfung in M1)
- [ ] TUnit unter .NET 10: Test-Runner-Konfiguration und CI-Aufruf werden in M1 verifiziert
- [ ] Repository-Name und Name der App
- [ ] Reale Dauer: Die Schätzung (ca. 15 Minuten) ist ungeprüft und wird erst in M7 gemessen

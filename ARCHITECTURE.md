# Architektur: ValueRanker

Dieses Dokument orientiert sich lose an [arc42](https://arc42.de), verwendet aber nur die Kapitel, die für dieses kleine Hobby-Projekt (ein Autor, ein Reviewer) tatsächlich Mehrwert bringen. Details zu Milestones und Anforderungen stehen in [`PLAN.md`](PLAN.md), Arbeitskonventionen in [`CLAUDE.md`](CLAUDE.md).

## 1. Einführung und Ziele

ValueRanker ist eine rein clientseitige Web-App, mit der eine Person aus einer Liste persönlicher Werte (Arbeitswerte) ihre Top-3 und eine verlässliche Top-10 ermittelt. Statt alle Werte einzeln zu sortieren, beantwortet man kleine Gruppen-Fragen ("wichtigster/unwichtigster Wert dieser Gruppe") und am Ende einige direkte Duelle für die Spitzenplätze – in rund 15 Minuten.

Zielgruppe dieses Dokuments: der Autor selbst, für spätere Wiedereinstiege ins Projekt.

## 2. Randbedingungen

- Blazor WebAssembly, .NET 10 – läuft komplett im Browser, kein Server, keine Datenbank.
- Hosting auf GitHub Pages unter einem Repository-Sub-Path.
- UI-Bibliothek: MudBlazor. Tests: TUnit (Core), Playwright (wenige Smoke-Tests).
- Architekturgrenze aus `CLAUDE.md`: Ports & Adapters mit bewusst nur zwei Projekten, keine zusätzlichen Schichten (DTO-Mapping, CQRS, generische Repositories) ohne Rücksprache.

## 3. Kontextabgrenzung

Ein Nutzer interagiert direkt im Browser mit der App. Es gibt keine externen Systeme – nur zwei lokale Datenquellen:

- **Statische Wertelisten** (`wwwroot/data/values.{de,en}.json` und `.compact.json`) – werden einmal per HTTP vom eigenen Origin geladen, danach im Speicher gecacht.
- **`localStorage`** des Browsers – einziger Persistenzort für laufende und abgeschlossene Läufe.

## 4. Lösungsstrategie

### Ports & Adapters

Zwei Projekte:

- **`ValueRanker.Core`** – `Domain/`, `Application/` (`RankingService`, der einzige Einstiegspunkt für die UI), `Ports/` (`IRunRepository`, `IValueListProvider`, `IClock`). Keine Abhängigkeit auf Blazor oder den Browser, dadurch vollständig mit TUnit testbar.
- **`ValueRanker.Web`** – Blazor-Komponenten (`Pages/`) und `Adapters/` (`LocalStorageRunRepository`, `HttpValueListProvider`, `SystemClock`). Komponenten sprechen ausschliesslich mit `RankingService`, nie direkt mit Domain-Klassen wie `RankingStrategy` oder `EloCalculator`.

### Event-Sourcing-orientiert statt Zustandsmutation

Ein Lauf (`RankingRun`) speichert nur `Seed` + eine Liste von `RankingEvent`s (Gruppen-Antwort, Duell-Antwort). Der komplette Zustand (Bewertungen, Phase, nächster Schritt) wird bei jedem Zugriff frisch berechnet:

```
RankingState.CreateInitial(seed, valueIds)  →  events.Aggregate(initial, strategy.Apply)  →  aktueller RankingState
```

Bewusst nur *orientiert* an Event Sourcing, nicht "echtes" Event Sourcing: es gibt keinen expliziten Event Store, keine Event Streams und keine Conditional Appends (optimistisches Nebenläufigkeits-Handling) – nur ein serialisiertes Events-Array pro Lauf in `localStorage`. Für dieses Projekt reicht das; die Prinzipien (Zustand aus Events ableiten statt mutieren) sind aber dieselben. Daraus folgt fast automatisch:
- **Rückgängig** = letztes Event entfernen, neu berechnen.
- **Deterministisch und gut testbar**, weil derselbe Seed + dieselben Events immer denselben Zustand ergeben.
- **Export** (JSON/CSV/Druckansicht) ist trivial, weil `RankingService.GetResultAsync` ohnehin eine fertige, serialisierbare Struktur liefert.

### Drei-Phasen-Strategie

`RankingStrategy` (siehe `IRankingStrategy`) führt einen Lauf durch drei Phasen:

1. **Build** – Gruppen à `GroupSize` (aktuell 4) Werten, Aufgabe "wichtigster/unwichtigster" (Best/Worst, 2 Taps). Ziel: schnell eine grobe Einordnung aller Werte.
2. **Focus** – Gruppen aus dem oberen Feld, Aufgabe "vollständige Reihenfolge" (Full-Order, 3 von 4 Taps, der letzte ergibt sich). Präziser als Best/Worst, aber teurer pro Wert – daher nur für die vielversprechenderen Werte.
3. **Finale** – einzelne Duelle für die Top-Kandidaten, bis Top-3 sich über mehrere Duelle stabilisiert haben oder ein Duell-Limit erreicht ist.

Jede Antwort wird über Elo-Ratings ausgewertet (`EloCalculator`), mit einem **adaptiven K-Faktor**: 48 solange ein Wert weniger als 5 Vergleiche hat (schnelle Anfangskorrektur), 24 bis 15 Vergleiche (Übergang), danach 12 (stabil, damit sich das Finale nicht mehr aufschaukelt).

### Wie eine Gruppen-Antwort intern bewertet wird

Eine Gruppe wird nicht "als Gruppe" bewertet, sondern in mehrere **paarweise Duelle** zerlegt (`GroupEventToPairwiseComparisons`), die dann genau wie ein normales Duell durch den Elo-Rechner laufen:

- **Best/Worst** (4 Werte) → 5 Duelle: der Beste gegen jeden der anderen drei, plus beide "Mittleren" gegen den Schlechtesten. So bekommen auch die nicht angetippten Werte wenigstens eine informative Information ("besser als der Schlechteste"), statt komplett unverändert zu bleiben.
- **Full-Order** (4 Werte) → 6 Duelle: vollständiger Rundlauf zwischen allen Paaren gemäss der angegebenen Reihenfolge.

Diese Duelle werden **sequenziell** angewendet: Jedes Duell aktualisiert die Bewertungen sofort, das nächste Duell rechnet bereits mit den neuen Werten. Das hat einen spürbaren, aber bewusst in Kauf genommenen Nebeneffekt: Der Effekt einer Antwort auf einen nicht direkt angetippten Wert hängt leicht von der internen Reihenfolge ab, in der die Duelle abgearbeitet werden – nicht von etwas, das inhaltlich mit dem Wert zu tun hat.

**Beispiel** (realer Log-Output, Gruppe "Erfahrung" (bester), "Ausdauer", "Loyalität", "Geduld" (schlechtester), alle vorher bei 1000):

```
Erfahrung: 1000.0 -> 1067.2 (+67.2), Vergleiche: 3
Ausdauer:  1000.0 -> 1001.5 (+1.5),  Vergleiche: 2
Loyalität: 1000.0 -> 1001.3 (+1.3),  Vergleiche: 2
Geduld:    1000.0 ->  930.0 (-70.0), Vergleiche: 3
```

Naiv erwartet man, dass Ausdauer und Loyalität exakt unverändert bleiben (nur der Beste gewinnt, nur der Schlechteste verliert). Tatsächlich gewinnen beide leicht, weil sie im zweiten Teil der Zerlegung je ein Duell gegen den (zu diesem Zeitpunkt bereits geschwächten) Geduld "gewinnen" – das ist gewollt. Dass die beiden aber nicht exakt symmetrisch bei 0 landen (+1.5 vs. +1.3), sondern eine kleine, ungleiche Restgrösse behalten, liegt an der sequenziellen Verarbeitung: Erfahrungs Sieg gegen Geduld passiert zuerst und verschiebt beide Ratings, bevor Ausdauer und Loyalität überhaupt an die Reihe kommen. Eine Alternative (alle Duelle einer Antwort gegen denselben Vorher-Stand berechnen statt verkettet) würde dieses Rauschen beseitigen, wurde aber bewusst nicht umgesetzt – siehe [Kapitel 9](#9-architekturentscheidungen).

### Simulationsgetriebenes Tuning

Konstanten wie `GroupSize`, die K-Faktor-Schwellen oder die Pool-Grössen pro Phase wurden nicht geraten, sondern mit einem virtuellen Nutzer (`RankingSimulation`/`VirtualUser`, `tests/ValueRanker.Core.Tests/Simulation/`) empirisch bestimmt: viele simulierte Läufe mit bekannter "wahrer" Zielreihenfolge und einstellbarem Rauschen, gemessen wird die Trefferquote der Top-3 gegen die Anzahl benötigter Taps. So wurde `GroupSize` zunächst auf 5 erhöht (bessere Genauigkeit bei ~250 Werten), später für die kompakte Liste wieder bewusst auf 4 gesenkt (Kompromiss: schneller für die kompakte Liste, etwas weniger genau für die volle Liste).

## 5. Bausteinsicht

```
ValueRanker.Core                          ValueRanker.Web
├── Domain/                                ├── Pages/
│   ├── RankingStrategy (IRankingStrategy) │   ├── Home.razor      (Laufliste, neuer Lauf)
│   ├── EloCalculator                      │   ├── Run.razor       (Taps, Fortschritt)
│   ├── GroupEventToPairwiseComparisons    │   ├── Result.razor    (Rangliste, Export)
│   ├── RankingState / RankingRun          │   └── NotFound.razor
│   ├── RankingPhaseCalculator             ├── Adapters/
│   └── ValueList / ValueListJsonLoader    │   ├── LocalStorageRunRepository
├── Application/                           │   ├── HttpValueListProvider
│   ├── RankingService  ← einziger         │   └── SystemClock
│   │   Einstiegspunkt für die UI          └── wwwroot/
│   └── RankingServiceModels (DTOs)            ├── data/*.json (Wertelisten)
└── Ports/                                     └── js/*.js     (JS-Interop-Module)
    ├── IRunRepository
    ├── IValueListProvider
    └── IClock
```

`RankingService` ist der einzige Punkt, den Blazor-Komponenten kennen. Er lädt einen Lauf über `IRunRepository`, repliziert den Zustand über `RankingStrategy`, holt Wertetexte über `IValueListProvider` und gibt fertige, UI-taugliche DTOs zurück (nie Domain-Typen direkt).

## 6. Laufzeitsicht: eine Best/Worst-Antwort

1. Nutzer tippt in `Run.razor` zuerst den wichtigsten, dann den unwichtigsten Wert einer Gruppe an.
2. `Run.razor` holt vorher einen Bewertungs-Snapshot (`RankingService.GetDebugSnapshotAsync`) für die Scoring-Transparenz.
3. `RankingService.SubmitGroupBestWorstAsync` lädt den Lauf, hängt ein `GroupBestWorstEvent` an und speichert ihn über `IRunRepository` (im Browser: `LocalStorageRunRepository`).
4. `Run.razor` holt den Snapshot erneut, vergleicht ihn mit dem vorherigen und loggt die Bewertungsänderungen in die Browser-Konsole (`Console.WriteLine` wird vom WASM-Runtime automatisch auf `console.log` umgeleitet).
5. `RankingService.GetNextStepAsync` repliziert den Zustand neu (inkl. des gerade angehängten Events) und liefert die nächste Gruppe oder das nächste Duell.
6. `Run.razor` rendert den nächsten Schritt; ist der Lauf fertig, wird direkt zu `Result.razor` weitergeleitet.

## 7. Verteilungssicht

- **CI/CD**: GitHub Actions (`.github/workflows/build-and-deploy.yml`) – `dotnet restore/build/test` (Release), dann `dotnet publish`, `<base href>` wird für den Repository-Sub-Path umgeschrieben, `index.html` wird zusätzlich als `404.html` kopiert (SPA-Fallback für Direktaufrufe von Unterseiten), `.nojekyll` verhindert Jekyll-Verarbeitung. Deploy über `actions/deploy-pages`.
- **Laufzeit**: keine eigene Infrastruktur – die App läuft komplett im Browser des Nutzers, einzige "Persistenzschicht" ist `localStorage`.

## 8. Querschnittliche Konzepte

**Lokalisierung** – `IStringLocalizer<AppStrings>` mit `AppStrings.resx` (Deutsch, Standard) und `AppStrings.en.resx`. Die Wertelisten sind pro Sprache eigenständig verfasst, keine 1:1-Übersetzung.

**Fehlerresilienz** – Beispiel: Wenn sich eine Werteliste inhaltlich ändert (wie es der kompakten Liste mehrfach passiert ist), können ältere Läufe auf nicht mehr existierende Werte-IDs verweisen. `RankingService.ListRunsAsync` fängt das pro Lauf ab und markiert nur den betroffenen Lauf als `IsCompatible = false`, statt die ganze Laufliste abstürzen zu lassen.

**Teststrategie** – TUnit für `ValueRanker.Core` (Domain- und Application-Logik über In-Memory-Fakes der Ports), inklusive der Simulationstests. Playwright nur für eine Handvoll durchgängige Smoke-Tests (kompletten Lauf durchklicken, JSON-Export prüfen), bewusst wenige und günstige – keine Deckungslogik, nur ein Beweis, dass UI → `RankingService` → Adapters nicht kaputt ist. Keine Adapter-Tests.

**JS-Interop** – kleine ES-Module unter `wwwroot/js/` (`blazorCulture.js`, `fileDownload.js`, `keyboardShortcuts.js`, `localStorageInterop.js`), zur Laufzeit per `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)` geladen, kein Eintrag in `Program.cs` nötig.

## 9. Architekturentscheidungen

- **Event-Sourcing-orientiert statt direkter Zustandsmutation** – kein "echtes" Event Sourcing (kein expliziter Event Store, keine Streams, keine Conditional Appends), aber dasselbe Grundprinzip; macht Rückgängig, Determinismus und Export nahezu kostenlos. Kosten ist eine Replay-Berechnung pro Lesezugriff, bei den hier üblichen Laufgrössen (wenige hundert Events) vernachlässigbar.
- **Nur zwei Projekte** (Core/Web) statt mehr Schichten – bewusste Reduktion für ein Hobby-Projekt; DTO-Mapping, CQRS oder generische Repositories würden hier nur Ballast erzeugen.
- **GroupSize 5 → 4** – ursprünglich 4, in M3 anhand von Simulationen auf 5 erhöht (bessere Genauigkeit bei ~250 Werten), nach Einführung der kompakten Liste wieder bewusst auf 4 gesetzt – ein dokumentierter Kompromiss zugunsten der kürzeren Liste (siehe `PLAN.md` §9).
- **Sequenzielle statt gleichzeitige Anwendung mehrerer Vergleiche pro Gruppen-Tap** – siehe Beispiel in Kapitel 4. Bewusst belassen: der Effekt ist klein und gleicht sich über einen Lauf aus; eine Umstellung auf gleichzeitige Berechnung wäre eine echte Änderung der Ranking-Strategie und würde eine erneute Simulation aller Konstanten erfordern (vergleichbar mit der GroupSize-Entscheidung), aktuell nicht priorisiert.
- **Keine Migration bei inkompatiblen Wertelisten** – bewusster Verzicht, da Werte über mehrere Revisionen inhaltlich zusammengelegt (nicht nur umbenannt) wurden, ohne eine Mapping-Dokumentation zu führen. Betroffene Läufe werden erkannt und können nur gelöscht werden.

## 10. Qualitätsanforderungen

- **Nachvollziehbarkeit** des Scorings: Konsolen-Log bei jeder Antwort, optionaler UI-Toggle mit den aktuellen Bewertungen aller Werte.
- **Resilienz** gegenüber inkompatiblen Altdaten im `localStorage` (siehe Kapitel 8).
- **Keine Backend-Abhängigkeit zur Laufzeit** – nach dem initialen Laden der App und der Wertelisten läuft die gesamte Logik im Browser, Persistenz ist rein lokal (`localStorage`). Kein Service Worker/PWA-Caching eingerichtet, ein vollständiger Reload lädt die App erneut vom Netz.

## 11. Risiken und technische Schulden

- Die sequenzielle Elo-Anwendung erzeugt reihenfolge-abhängiges Rauschen bei Gruppen-Antworten (siehe Kapitel 4/9) – akzeptiert und dokumentiert, nicht behoben.
- `RankingService.ListRunsAsync` repliziert bei jedem Aufruf der Startseite **alle** gespeicherten Läufe neu. Für die hier üblichen wenigen Läufe unproblematisch, würde bei sehr vielen Läufen aber spürbar langsamer.
- Änderungen am Inhalt einer Werteliste können bestehende Läufe inkompatibel machen (siehe Kapitel 8/9) – es gibt nur Resilienz, keine Migration.

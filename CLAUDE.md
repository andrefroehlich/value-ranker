# CLAUDE.md

Project: **ValueRanker**, a client-side Blazor WebAssembly app (.NET 10) that helps a user find their top work values through group rankings (best/worst, full order) followed by pairwise duels for the top values. Full plan: see `PLAN.md`. Read it before starting any work.

## Workflow

- Work on **one milestone at a time** as defined in `PLAN.md`. Do not work ahead.
- Before starting a milestone, state briefly what you intend to do. Ask if something is unclear instead of guessing.
- When a milestone is done: summarize the changes, show test results, list open questions, then **stop and wait for approval**.
- Make small, focused commits with English messages (imperative mood, e.g. "Add Elo calculator").
- The owner is an experienced .NET developer and reviews every step. Prefer simple, readable code over clever code.

## Conventions

- Code, identifiers, comments, commit messages, README: **English**.
- UI text: German and English via `IStringLocalizer` (resx). No hardcoded UI strings in components once milestone 6 is done.
- Value lists are data, not code: `wwwroot/data/values.de.json` and `values.en.json`. Each language has its own independent list (not a 1:1 translation).
- Nullable reference types enabled, warnings as errors in `Core`.
- Use `async`/`await` end to end, no `.Result` or `.Wait()` (WASM is single-threaded).
- Add NuGet packages only with a short justification.

## Architecture rules

- Ports and adapters, kept pragmatic: two production projects only. `ValueRanker.Core` holds `Domain/`, `Application/` (the `RankingService`, the single entry point for any UI or API) and `Ports/` (`IRunRepository`, `IValueListProvider`, `IClock`). `ValueRanker.Web` holds Blazor components and `Adapters/`.
- `ValueRanker.Core` has **no** dependency on Blazor, the browser, or JS interop. Blazor components depend only on `RankingService`, never on domain classes like Elo or strategies directly. Use case inputs and outputs are simple serializable records.
- Do not add extra layers (DTO mapping, MediatR/CQRS, generic repositories, additional projects) without asking.
- Tests: **TUnit**, for `ValueRanker.Core` only (domain logic and use cases via in-memory fakes of the ports). No automated tests for adapters. This was originally "no UI tests" full stop; loosened starting with M5 to allow a small number of Playwright end-to-end smoke tests for the Blazor UI (e.g. create a run, answer a group, reload, resume) — these check that the full pipe (UI → `RankingService` → adapters) isn't broken, not test coverage. Keep them few and cheap to maintain; do not use them to test domain logic (that stays in TUnit).
- Application state is derived by replaying an event log (`seed` + `events`). Undo removes the last event. Never mutate ratings directly outside the replay.
- Ranking behavior must be deterministic for a given seed. Randomness only via a seeded `Random`.
- Persistence goes through `IRunRepository`. The localStorage implementation lives in `ValueRanker.Web` and uses JS interop. Handle missing, corrupt, or blocked storage gracefully.
- No backend, no external network calls at runtime.

## Deployment

- Target: GitHub Pages via GitHub Actions. Must work with a repository sub-path (`<base href>`), and direct navigation to sub-routes must work (`404.html` SPA fallback, `.nojekyll`).

## Definition of done (per milestone)

- Builds without warnings, all tests green, CI green.
- Acceptance criteria of the milestone in `PLAN.md` are met and demonstrated.
- No unrelated changes in the diff.

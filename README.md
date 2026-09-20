# ValueRanker

A client-side Blazor WebAssembly app (.NET 10) that helps you find your
top personal work values. You rank small groups of values, best/worst
or in full order, and the final few values are settled by direct duels.
Everything runs in the browser — no backend, no accounts. Runs are
saved to the browser's local storage.

See [`PLAN.md`](PLAN.md) for the full design and milestone plan, and
[`CLAUDE.md`](CLAUDE.md) for the project's working conventions.

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/ValueRanker.Web
```

Then open the URL printed in the console (typically
`http://localhost:5000` or similar).

## Running the tests

```bash
dotnet test ValueRanker.slnx
```

This runs the TUnit tests in `ValueRanker.Core.Tests` (domain logic and
use cases) and the Playwright end-to-end smoke tests in
`ValueRanker.Web.SmokeTests` (a couple of checks that the UI, the
`RankingService`, and the storage adapters are wired together
correctly — not a substitute for the Core tests). The smoke tests
start their own instance of the app and a headless Chromium browser;
no extra setup is needed beyond the .NET SDK.

## Deployment

Pushing to `main` builds, tests, and deploys the app to GitHub Pages
via the workflow in `.github/workflows/build-and-deploy.yml`. The
workflow rewrites `<base href>` for the repository's sub-path and adds
a `404.html` fallback so direct navigation to a sub-route (e.g.
reloading on `/run/{id}`) works correctly on GitHub Pages.

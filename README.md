# StrategyNotifier – GEM (Global Equities Momentum)

StrategyNotifier runs quantitative strategies; the current implementation focuses on a daily-price, thesis-aligned GEM. The CLI is offline-first: calculations read from a local store, updates are optional and isolated, and tests never require network access.

## Quickstart
- Prerequisite: .NET 10 SDK.
- Run tests: `dotnet test`
- Run GEM without touching the network (recommended for CI/tests):  
  `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update`
- Run GEM with a manual refresh attempt (not for CI/tests; may hit the network):  
  `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update`
- Configuration is loaded from `config/gem/gem.config.json` (relative to the working directory). Paths can be relative; the store directory must already exist.

## Offline-first data flow
- **Store vs cache:** Strategy calculations read only from `storeDirectory`. Updates download into `cacheDirectory` and optionally promote into the store (`update.saveUpdatedDataToStore=true`).
- **Update modes:** `--no-update` skips any HTTP; `--force-update` always tries to refresh; no flag uses `update.autoUpdateEnabled` plus throttling (`minMinutesBetweenAttempts`).
- **Staleness:** `update.maxAgeDays` compares the last available data date to today (UTC); if stale, the updater may fetch. Per-symbol `.last_attempt` files in the cache prevent tight retry loops.
- Details: see [Data update flow](docs/Data-Update-Flow.md).

## Configuration at a glance
- Strict schema at `config/gem/gem.config.json`; unknown fields fail fast.
- Instruments: `ticker` is the display/portfolio identifier, `sourceSymbol` is the file/download key. CSV files in store/cache are named `<sourceSymbol>.csv` (lowercase recommended for Stooq).
- Directories: `storeDirectory` is the source of truth for calculations; `cacheDirectory` is for downloads and attempt metadata.
- Core fields: `windowMonths`, `rankingMode`, `instruments.usEquity|exUsEquity|safeAsset` (each: `ticker`, `name`, `sourceSymbol`), `storeDirectory`, `cacheDirectory`, `outputPath`.
- Update block: `update.autoUpdateEnabled`, `update.maxAgeDays`, `update.minMinutesBetweenAttempts`, `update.saveUpdatedDataToStore`.
- Full schema and defaults: [Configuration](docs/Configuration.md).

## Output at a glance
- Signals are written to the configured `outputPath` (e.g., `dist/gem/signals.json` in the default config).
- The list is newest-first (by signal date). Allocations are sorted by weight desc, then ticker asc. UTF-8 without BOM; formatting is stable for golden tests.
- Contract details: [Output contract](docs/Output-Contract.md).

## Algorithm & assumptions (summary)
- Momentum = total return over `windowMonths`, using daily close prices. Start/end prices are taken as the latest available on or before the target dates.
- Relative ranking compares US vs ex-US equities. `rankingMode=Top1` allocates 100% to the leader; `Top2` splits 50/50 between the top two risk-on instruments.
- Absolute momentum gate is always enabled with a 0.0 threshold; if the leader's return is <= 0, the portfolio moves to the safe asset.
- Signals are generated on month-end checkpoints across the intersection of available prices; outputs are sorted newest-first. Full detail: [Algorithm & assumptions](docs/Algorithm-and-Assumptions.md).

## Thesis mapping
| Thesis element | Where it lives |
| --- | --- |
| Daily-price GEM engine & momentum math | `src/Strategies/Gem/Gem.Domain/Engine/GemEngine.cs`, `src/Strategies/Gem/Gem.Domain/Model/*` |
| Config schema & validation | `src/Strategies/Gem/Gem.Cli/Configuration/*`, `config/gem/gem.config.json` |
| Offline-first store/cache & updater | `src/Strategies/Gem/Gem.Cli/Pricing/*` |
| CLI orchestration & output writing | `src/Strategies/Gem/Gem.Cli/Execution/GemRunner.cs`, `src/Strategies/Gem/Gem.Cli/Contracts/*` |
| Golden tests & locked outputs | `tests/Strategies/Gem/Gem.Cli.Tests/GemCliGoldenTests.cs`, `tests/Strategies/Gem/Gem.Cli.Tests/TestData/Golden/*` |
Full mapping: [Thesis mapping](docs/Thesis-Mapping.md).

## Examples
- Sample CSVs included in the repo (under `data/gem/sample`): `voo.us`, `veu.us`, `agg.us`; `sppw.de`, `is3n.de`, `euna.de`; `etfbw20tr.pl`, `etfbm40tr.pl`, `etfbndxpl.pl`. To run a given set, ensure `config/gem/gem.config.json` instruments use matching `sourceSymbol` values so that `<sourceSymbol>.csv` resolves in your `storeDirectory`. More guidance: [Examples](docs/Examples.md).

## Testing notes
- Unit tests cover config validation, CSV parsing, engine math, and CLI behaviors (update flags, store/cache promotion, output encoding).
- Golden/E2E tests lock ordering, encoding (UTF-8 without BOM), invariant culture formatting, and deterministic sorting of signals/allocations.
- Tests and CI must not hit the network; use `--no-update` or inject fake updaters (as in test fixtures).

## Roadmap
- Automate publishing (Pages/CI) for docs and artifacts.
- Optional notifications (e.g., Apps Script) consuming `signals.json`.
- Additional strategies alongside GEM while reusing the offline-first pipeline.

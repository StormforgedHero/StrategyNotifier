# Thesis Mapping

| Thesis concept / section | Repository path(s) | Notes |
| --- | --- | --- |
| Momentum lookback, relative vs absolute rules | `src/Strategies/Gem/Gem.Domain/Engine/GemEngine.cs`, `.../Model/MomentumParameters.cs`, `.../Model/Signal.cs` | Implements daily-price momentum over `windowMonths`, `rankingMode` (`Top1`/`Top2`), and the absolute momentum gate (threshold 0.0, always enabled). |
| Instruments and identifiers | `src/Strategies/Gem/Gem.Domain/Model/Instrument.cs` | Manages `ticker` vs `sourceSymbol`, normalized symbols for file naming and downloads. |
| Configuration schema & validation | `src/Strategies/Gem/Gem.Cli/Configuration/*`, `config/gem/gem.config.json` | Strict field allowlist, defaults (`windowMonths`, `rankingMode`, update settings), unique identifiers, required instruments, path normalization. |
| Data ingestion (CSV parsing) | `src/Strategies/Gem/Gem.Domain/Pricing/PriceCsvParser.cs`, `src/Strategies/Gem/Gem.Cli/Pricing/LocalCsvPriceDataProvider.cs` | Robust CSV parsing (delimiter detection, BOM, quoted fields, culture-aware decimals, Stooq header variants). |
| Offline-first data storage | `src/Strategies/Gem/Gem.Cli/Pricing/FilePriceSeriesRepository.cs` | Reads only from `storeDirectory`; cache recency does not override store data; uses `sourceSymbol`-derived file names. |
| Update pipeline (HTTP, staleness, cooldown) | `src/Strategies/Gem/Gem.Cli/Pricing/PriceDataUpdater.cs`, `StooqCsvHttpDataProvider.cs` | Per-symbol staleness against `maxAgeDays`, per-symbol cooldown via `.last_attempt`, optional promotion to store, atomic writes, 1s pacing between requests. |
| CLI orchestration & flags | `src/Strategies/Gem/Gem.Cli/Execution/GemRunner.cs`, `Program.cs` | Handles `--no-update` / `--force-update`, builds signal history, writes output with deterministic ordering and encoding. |
| Output contract | `src/Strategies/Gem/Gem.Cli/Contracts/*`, `GemRunner.WriteSignals` | CamelCase fields, allocation sorting rules, newest-first ordering, UTF-8 without BOM. |
| Golden expectations & end-to-end behavior | `tests/Strategies/Gem/Gem.Cli.Tests/GemCliGoldenTests.cs`, `GemCliEndToEndTests.cs`, `.../TestData/Golden` | Locks output ordering, encoding, deterministic formatting, culture invariance. |
| Update throttling & cache/store promotion tests | `tests/Strategies/Gem/Gem.Cli.Tests/GemRunnerUpdateFlagsTests.cs`, `GemRunnerCacheTests.cs`, `GemCachePromotionTests.cs`, `GemPriceDataUpdaterPersistenceTests.cs`, `FilePriceSeriesRepositorySelectionTests.cs` | Verifies offline-first rules, promotion toggles, staleness logic, per-symbol cooldown, and store dominance. |
| Input validation & exit codes | `tests/Strategies/Gem/Gem.Cli.Tests/ProgramExitCodeTests.cs`, `GemConfigLoaderTests.cs`, `ProgramArgumentValidationTests.cs` | Ensures strict schema handling, clear exit codes, and defensive argument validation. |

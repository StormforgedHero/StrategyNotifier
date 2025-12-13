# StrategyNotifier

StrategyNotifier is a .NET-based tool for running and notifying about quantitative investment strategies.  
The first implemented strategy is **GEM (Global Equities Momentum)**.

The repository is structured so that GEM is just one strategy under a broader umbrella; future strategies can be added alongside GEM without changing the core layout.

---

## Technology stack

- .NET 10 (SDK)
- C#
- xUnit for unit tests
- Visual Studio 2026
- Planned deployment targets (later phases):
  - GitHub Actions
  - GitHub Pages

---

## Repository layout

### Solution structure

    StrategyNotifier.sln
    src/
      Strategies/
        Gem/
          Gem.Domain/
          Gem.Cli/
    tests/
      Strategies/
        Gem/
          Gem.Domain.Tests/
          Gem.Cli.Tests/
    config/
      gem/
        gem.cli.sample.json
    data/
      gem/
        sample/
        raw/            (ignored)
    dist/
      gem/             (.gitkeep; generated files ignored)

### `src/Strategies/Gem/Gem.Domain`

GEM domain model and core engine:

- `Core` – fundamental types and series:
  - `YearMonth` – immutable representation of a year-month period
  - `MonthlyReturn` – monthly return for a given period
  - `AssetKind` – US equity, ex-US equity, safe asset
  - `AssetReturnSeries` – ordered, validated series of monthly returns (sorted and checked for duplicates)
- `Model` – strategy-specific types:
  - `GemParameters` – GEM configuration (lookback window in months)
  - `GemInputData` – input container for all three asset series
  - `GemSignal` – monthly allocation decision (period + chosen asset kind)
- `Engine` – `GemEngine` implementing core GEM logic:
  - computes rolling lookback windows for all three assets
  - evaluates relative momentum between US and ex-US equities
  - evaluates absolute momentum versus the safe asset
  - produces a sequence of monthly allocation decisions for all eligible periods
- `Exceptions` – `DomainValidationException` for domain-level validation failures

### `src/Strategies/Gem/Gem.Cli`

Configuration-driven console application for running GEM end-to-end:

- `Program.cs`
  - loads configuration from `config/gem/gem.cli.json` (relative to current working directory)
  - executes `GemRunner`
  - prints a concise summary (paths, lookback, signal count)
  - returns exit code `0` on success and non-zero on configuration/data/IO errors

- `Configuration/`
  - `GemCliConfiguration`
    - strongly-typed model for CLI settings
    - `Validate()` enforces required values and positive `LookbackMonths`
  - `GemConfigLoader`
    - loads JSON configuration (case-insensitive mapping)
    - validates configuration via `GemCliConfiguration.Validate()`
    - surfaces problems as:
      - missing file (`FileNotFoundException`)
      - empty file (`InvalidOperationException`)
      - invalid JSON (`InvalidOperationException` with “invalid JSON” hint)
      - invalid configuration values (`InvalidOperationException`)

- `Contracts/`
  - `GemSignalOutput`
    - output contract in `signals.json`:
      - `period` – `YYYY-MM`
      - `position` – `UsEquity | ExUsEquity | SafeAsset`

- `Execution/`
  - `GemRunner`
    - orchestrates the full run:
      - loads CSV input via `GemCsvInputLoader`
      - runs `GemEngine.GenerateSignals`
      - maps domain signals to `GemSignalOutput`
      - writes indented JSON to `OutputSignalsFile`
      - writes output as UTF-8 without BOM

- `IO/`
  - `GemCsvInputLoader`
    - loads monthly return series from CSV files (US, ex-US, safe asset)
    - supports robust inputs used in tests:
      - skips blank lines and comment lines (`#` and `//`) also before header
      - detects delimiter (`;` or `,`) based on header
      - tolerates whitespace around values
      - supports quoted values and escaped quotes
      - supports comma decimal separator (`0,02`) and dot decimal separator (`0.02`)
      - strips trailing comments in the Return column (`0.02 # note`, `0.02 // note`)
      - tolerates extra columns (uses first three: Year, Month, Return)
      - handles UTF-8 BOM in header
    - throws:
      - `FileNotFoundException` when an expected CSV file is missing
      - `FormatException` when structure or numeric values are invalid

---

## Exit codes

`Gem.Cli.Program` uses explicit exit codes to make automation reliable:

- `0`  – success
- `10` – configuration file not found
- `11` – configuration error (invalid JSON or invalid configuration values)
- `20` – input data file not found
- `21` – input data format error (CSV parsing/format issues)
- `22` – input data validation error (domain validation)
- `30` – output write error (likely related to output path/permissions)
- `31` – other I/O error
- `99` – unexpected error

---

## Tests

### `tests/Strategies/Gem/Gem.Domain.Tests`

Unit tests for the GEM domain and engine:

- `Core` – `YearMonth`, `AssetReturnSeries` (sorting, duplicates, lookbacks)
- `Model` – `GemParameters` validation, `GemInputData` invariants
- `Engine` – basic scenarios and edge cases
- `TestData` – `GemTestDataFactory` helper for synthetic series

### `tests/Strategies/Gem/Gem.Cli.Tests`

Tests focused on CLI behavior:

- `Configuration/`
  - `GemCliConfigurationTests`
  - `GemConfigLoaderTests`

- `IO/`
  - `GemCsvInputLoaderTests`
  - `GemCsvInputLoaderRobustnessTests`

- `Execution/`
  - `GemRunnerTests`

- `Contracts/`
  - `SignalsJsonContractTests`

- `EntryPoint/` (tests for the CLI entry point without namespace collisions with `Gem.Cli.Program`)
  - `ProgramRunTests`
  - `ProgramExitCodeTests`
  - `ProgramSuccessOutputTests`
  - `ProgramInputDataFormatTests`
  - `ProgramDomainValidationTests`
  - `ProgramIoErrorTests`
  - `ProgramOutputWriteTests`
  - `ProgramOutputContractTests`
  - `ProgramNoSignalsTests`
  - `ProgramSmokeTests` (minimal end-to-end “happy path”)

---

## Configuration

Configuration files live under `config/gem`:

- `config/gem/gem.cli.sample.json`  
  Reference configuration template:

    {
      "dataDirectory": "data/gem/sample",
      "usEquityFile": "us-equity.csv",
      "exUsEquityFile": "exus-equity.csv",
      "safeAssetFile": "safe-asset.csv",
      "outputSignalsFile": "dist/gem/signals.json",
      "lookbackMonths": 12
    }

- `config/gem/gem.cli.json`  
  User-specific config (ignored by Git). Typical usage:

  1) Copy the sample file:

       cp config/gem/gem.cli.sample.json config/gem/gem.cli.json

  2) Adjust paths/settings (e.g. switch data to `data/gem/raw`).

The CLI always looks for `config/gem/gem.cli.json` relative to the current working directory.

---

## Data folders

- `data/gem/sample/`  
  Synthetic, versioned sample data:

  - `us-equity.csv`
  - `exus-equity.csv`
  - `safe-asset.csv`

  CSV format:

    Year,Month,Return
    2025,1,0.02
    2025,2,0.03
    2025,3,-0.01

- `data/gem/raw/`  
  Reserved for real market data; ignored by Git.

---

## Output folder

- `dist/gem/`  
  Output directory for GEM artifacts:

  - `.gitkeep` – keeps the directory in Git
  - `signals.json` – generated output (ignored by Git)

### `signals.json` format

The CLI writes `signals.json` as a JSON array of objects:

- `period` – `YYYY-MM`
- `position` – `UsEquity`, `ExUsEquity`, or `SafeAsset`

Example:

    [
      {
        "period": "2025-03",
        "position": "UsEquity"
      },
      {
        "period": "2025-04",
        "position": "SafeAsset"
      }
    ]

---

## Building and testing

From the repository root:

- Build:

    dotnet build

- Run all tests:

    dotnet test

---

## Running GEM from the CLI

1) Ensure you have a valid config:

    cp config/gem/gem.cli.sample.json config/gem/gem.cli.json

2) Make sure the configured data directory and CSV files exist.

3) Run:

    dotnet run --project src/Strategies/Gem/Gem.Cli/Gem.Cli.csproj

The app will load configuration, read CSVs, run GEM, write `signals.json`, and print a summary.

---

## Current status

The project is currently at **Phase 4**:

- CLI and domain logic are stable and fully covered by unit tests.
- CSV input pipeline has been hardened (delimiter detection, comments, quoting, comma decimals, BOM handling).
- Output writing guarantees UTF-8 without BOM and deterministic, indented JSON.
- Program error handling is mapped to explicit exit codes and verified by tests.

Next phases will focus on CI/CD and presentation/consumption of `signals.json`.

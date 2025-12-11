# StrategyNotifier

StrategyNotifier is a .NET-based tool for running and notifying about quantitative investment strategies.  
The first implemented strategy is **GEM (Global Equities Momentum)**.

The project is structured so that GEM is just one strategy under a broader umbrella; future strategies can be added alongside GEM without changing the core layout.

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

### Core solution structure

- `src/Strategies/Gem/Gem.Domain`  
  GEM domain model and core engine:

  - `Core` – fundamental value objects and series:
    - `YearMonth` – immutable representation of a year-month period
    - `MonthlyReturn` – monthly return for a given period
    - `AssetKind` – US equity, ex-US equity, safe asset
    - `AssetReturnSeries` – ordered, validated series of monthly returns (sorted and checked for duplicates)
  - `Model` – strategy-specific data contracts:
    - `GemParameters` – GEM configuration (lookback window in months)
    - `GemInputData` – input container for all three asset series
    - `GemSignal` – monthly allocation decision (period + chosen asset kind)
  - `Engine` – `GemEngine` implementing core GEM logic:
    - computes rolling lookback windows for all three assets
    - evaluates relative momentum between US and ex-US equities
    - evaluates absolute momentum versus the safe asset
    - produces a sequence of monthly allocation decisions for all eligible periods
  - `Exceptions` – `DomainValidationException` for domain-level validation failures

- `src/Strategies/Gem/Gem.Cli`  
  Configuration-driven console application for running GEM end-to-end:

  - `Program.cs`
    - uses the repository root as the working directory
    - loads configuration from `config/gem/gem.cli.json`
    - executes the `GemRunner` with the loaded configuration
    - prints a concise summary (config path, data directory, output path, lookback, signal count)
    - returns exit code `0` on success and non-zero on configuration or data errors

  - `Configuration/`
    - `GemCliConfiguration`
      - strongly-typed model for GEM CLI settings:
        - `DataDirectory` – base directory for CSV input data
        - `UsEquityFile`, `ExUsEquityFile`, `SafeAssetFile` – filenames for the three asset series
        - `OutputSignalsFile` – target path for `signals.json`
        - `LookbackMonths` – lookback window in months
      - `Validate()` enforces non-empty values and positive `LookbackMonths`
    - `GemConfigLoader`
      - loads JSON configuration from a given file path (typically `config/gem/gem.cli.json`)
      - uses case-insensitive property mapping
      - validates the configuration using `GemCliConfiguration.Validate()`
      - handles:
        - missing file (`FileNotFoundException`)
        - empty file
        - invalid JSON
        - invalid configuration values (wrapped in `InvalidOperationException`)

  - `Contracts/`
    - `GemSignalOutput`
      - output contract for a single GEM signal in `signals.json`:
        - `period` – string in `YYYY-MM` format
        - `position` – string representation of the chosen `AssetKind` (`UsEquity`, `ExUsEquity`, `SafeAsset`)

  - `Execution/`
    - `GemRunner`
      - orchestrates the full GEM run based on `GemCliConfiguration`:
        - builds input paths from `DataDirectory` and configured filenames
        - uses `GemCsvInputLoader` to load `GemInputData`
        - creates `GemParameters` from `LookbackMonths`
        - runs `GemEngine.GenerateSignals`
        - maps domain `GemSignal` objects to `GemSignalOutput` DTOs
        - ensures the output directory exists and writes a pretty-printed `signals.json` to `OutputSignalsFile`
      - returns the number of generated signals so that `Program` can report it

  - `IO/`
    - `GemCsvInputLoader`
      - loads GEM input data from three CSV files:
        - US equity
        - ex-US equity
        - safe asset
      - constructor takes a base directory
      - `Load()` overload:
        - uses conventional filenames: `us-equity.csv`, `exus-equity.csv`, `safe-asset.csv`
      - `Load(usEquityFileName, exUsEquityFileName, safeAssetFileName)` overload:
        - uses filenames provided by `GemCliConfiguration`
      - CSV format:
        - header row: `Year,Month,Return`
        - one row per month
        - `Return` is a monthly rate (e.g. `0.02` = 2%)
      - parses numeric values using invariant culture
      - builds `MonthlyReturn` and `AssetReturnSeries` for each asset
      - throws:
        - `FileNotFoundException` when an expected CSV file is missing
        - `FormatException` when any row has invalid structure or non-numeric values

---

## Tests

- `tests/Strategies/Gem/Gem.Domain.Tests`  
  Unit tests for the GEM domain and engine:

  - `Core`
    - tests for `YearMonth` construction, comparison and `AddMonths` behavior
    - tests for `AssetReturnSeries` sorting, duplicate detection and lookback windows
  - `Model`
    - tests for `GemParameters` validation (lookback window must be positive)
    - tests for `GemInputData` invariants (null checks and `AssetKind` validation)
  - `Engine`
    - `GemEngineBasicScenariosTests`
      - scenarios where each asset type (US, ex-US, safe) always wins
    - `GemEngineEdgeCasesTests`
      - insufficient history
      - null arguments
      - regime shifts (switching to safe asset when it becomes superior)
      - ties between US and ex-US (preference for US)
      - empty series handling
  - `TestData`
    - `GemTestDataFactory` – helper for building synthetic return series for tests

- `tests/Strategies/Gem/Gem.Cli.Tests`  
  Tests focused on CLI-specific behavior:

  - `Configuration/`
    - `GemCliConfigurationTests`
      - validates that a well-formed configuration passes `Validate()`
      - verifies failures for:
        - empty or whitespace `DataDirectory`
        - empty or whitespace input filenames
        - empty or whitespace `OutputSignalsFile`
        - `LookbackMonths <= 0`
    - `GemConfigLoaderTests`
      - covers:
        - missing configuration file (`FileNotFoundException`)
        - valid configuration JSON and successful deserialization
        - invalid JSON (reported as configuration error)
        - invalid `lookbackMonths` value (validation failure)

  - `Execution/`
    - `GemRunnerTests`
      - verifies that a valid configuration and CSV data:
        - produce `signals.json` at the configured path
        - result in a non-zero signal count
      - checks that:
        - deserialized `GemSignalOutput` list has non-empty periods and positions
        - specific sample inputs lead to expected signals, for example:
          - `2025-02: UsEquity`
          - `2025-03: UsEquity`
        when using a 2-month lookback

  - `IO/`
    - `GemCsvInputLoaderTests`
      - verifies successful loading from valid CSV files
      - checks behavior when files are missing (`FileNotFoundException`)
      - checks behavior when numeric values are malformed (`FormatException`)
      - verifies that header-only files produce empty series

  - `ProgramTests`
    - `Main` behavior in end-to-end scenarios:
      - missing `gem.cli.json`:
        - non-zero exit code
        - error output containing "Configuration file not found"
      - valid configuration and sample data:
        - exit code `0`
        - standard output contains "StrategyNotifier - GEM CLI" and "Generated signals"
        - `dist/gem/signals.json` is created and contains at least one signal
      - invalid configuration values (e.g. `lookbackMonths: 0`):
        - non-zero exit code
        - error output contains "Configuration error" and a relevant hint
      - invalid configuration JSON:
        - non-zero exit code
        - error output contains a configuration error mentioning invalid JSON
      - missing input data file (e.g. no `safe-asset.csv`):
        - non-zero exit code
        - error output contains "Input data file not found" and the missing filename

---

## Configuration

Configuration files live under `config/gem`:

- `config/gem/gem.cli.sample.json`  
  Sample GEM CLI configuration file that documents the expected structure and defaults:

    {
      "dataDirectory": "data/gem/sample",
      "usEquityFile": "us-equity.csv",
      "exUsEquityFile": "exus-equity.csv",
      "safeAssetFile": "safe-asset.csv",
      "outputSignalsFile": "dist/gem/signals.json",
      "lookbackMonths": 12
    }

  This file is part of the repository and serves as a template.

- `config/gem/gem.cli.json`  
  User-specific CLI configuration file (ignored by Git).  
  Typical usage:

  1. Copy the sample file:

         cp config/gem/gem.cli.sample.json config/gem/gem.cli.json

  2. Adjust paths and settings to match your local environment, for example:
     - switch `dataDirectory` from `data/gem/sample` to `data/gem/raw`
     - change `lookbackMonths` from `12` to `3` if you want to experiment

The CLI always looks for `config/gem/gem.cli.json` relative to the current working directory.

---

## Data folders

- `data/gem/sample/`  
  Synthetic, versioned sample data used for local runs and tests:

  - `us-equity.csv`
  - `exus-equity.csv`
  - `safe-asset.csv`

  CSV format:

    Year,Month,Return
    2025,1,0.02
    2025,2,0.03
    2025,3,-0.01
    ...

  Semantics:

  - one row per calendar month
  - `Return` is a monthly rate (e.g. `0.02` = 2%)

- `data/gem/raw/`  
  Reserved for real market data (for example, exported from external data providers).  
  This directory is ignored by Git so that real, possibly proprietary data never enters version control.

---

## Output folder

- `dist/gem/`  
  Output directory for GEM-related artifacts:

  - `.gitkeep` – ensures the directory structure is tracked in Git
  - `signals.json` – JSON file with generated GEM signals

  The `dist/` tree is excluded from version control, so `signals.json` and other generated artifacts are not committed.

### `signals.json` format

The CLI writes `signals.json` as a JSON array of objects. Each object has:

- `period` – string in `YYYY-MM` format
- `position` – one of `UsEquity`, `ExUsEquity`, `SafeAsset`

Example shape:

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

Future phases may extend each element with additional fields (e.g. momentum metrics), but the existing fields will remain stable.

---

## Current status

The project is currently at **Phase 3**:

- Solution, projects and shared build configuration are in place.
- GEM domain model and engine are fully implemented and covered with unit tests.
- CSV-based input pipeline is implemented and reusable via `GemCsvInputLoader`.
- The CLI is now configuration-driven:
  - settings are loaded from `config/gem/gem.cli.json`
  - invalid configuration or JSON is reported with clear error messages
- Full vertical slice is completed:
  - CSV input (monthly returns)
  - GEM engine execution
  - JSON signals output (`dist/gem/signals.json`)
  - end-to-end behavior covered by tests

Future phases will introduce:

- static frontend consuming `signals.json`
- CI/CD with GitHub Actions
- publishing via GitHub Pages
- optional integration with external schedulers or notification channels

---

## Building and testing

From the repository root:

- Build the solution:

      dotnet build

- Run all tests:

      dotnet test

---

## Running GEM from the CLI

1. Ensure you have a valid CLI configuration:

   - Create or update `config/gem/gem.cli.json`.
   - For example, you can base it on the sample:

         cp config/gem/gem.cli.sample.json config/gem/gem.cli.json

   - Adjust paths and `lookbackMonths` if needed.

2. Make sure the configured data directory and CSV files exist.  
   For example, if you use the sample configuration, ensure:

   - `data/gem/sample/us-equity.csv`
   - `data/gem/sample/exus-equity.csv`
   - `data/gem/sample/safe-asset.csv`

3. From the repository root, run the GEM CLI:

      dotnet run --project src/Strategies/Gem/Gem.Cli/Gem.Cli.csproj

The application will:

- load configuration from `config/gem/gem.cli.json`
- load monthly returns from the three CSV files
- apply the GEM engine with the configured lookback window
- write `signals.json` to the configured output path
- print a summary similar to:

    StrategyNotifier - GEM CLI
    Configuration file       : /path/to/repo/config/gem/gem.cli.json
    Data directory           : data/gem/sample
    Output file              : dist/gem/signals.json
    Lookback window (months) : 2
    Generated signals        : 2

If something is misconfigured or a data file is missing, the CLI will:

- write a descriptive error message to standard error, and
- terminate with a non-zero exit code.

As the project evolves, the same `signals.json` contract will be consumed by a static frontend, automation workflows and notification mechanisms.

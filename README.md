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
    - `AssetReturnSeries` – ordered, validated series of monthly returns
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
  Console application entry point for running GEM:
  - `Program.cs`
    - uses the repository root as the working directory
    - loads sample CSV data from `data/gem/sample`
    - builds `GemInputData` via the CSV loader
    - runs `GemEngine` with a configured lookback window (in Phase 2 sample runs this is set to 3 months)
    - prints a summary and generated signals to the console
  - `IO/GemCsvInputLoader.cs`
    - loads three CSV files: `us-equity.csv`, `exus-equity.csv`, `safe-asset.csv`
    - parses `Year`, `Month` and `Return` using invariant culture
    - builds `MonthlyReturn` collections and `AssetReturnSeries` instances
    - constructs a `GemInputData` instance for use by `GemEngine`
  - `Configuration/` (placeholders for later phases)
    - `GemCliConfiguration.cs` – planned CLI configuration model (not implemented yet)
    - `GemConfigLoader.cs` – planned loader for `gem.cli.json` (not implemented yet)
  - `Execution/` (placeholder for later phases)
    - `GemRunner.cs` – planned orchestration of configuration, input loading and output generation (not implemented yet)

---

### Tests

- `tests/Strategies/Gem/Gem.Domain.Tests`  
  Unit tests for the GEM domain and engine:
  - `Core`
    - tests for `YearMonth` construction, comparison and `AddMonths` behavior
    - tests for `AssetReturnSeries` sorting, duplicate detection and lookback windows
  - `Model`
    - tests for `GemParameters` validation (lookback window must be positive)
    - tests for `GemInputData` invariants (null checks and `AssetKind` validation)
  - `Engine`
    - `GemEngineBasicScenariosTests` – scenarios where each asset type (US, ex-US, safe) always wins
    - `GemEngineEdgeCasesTests` – insufficient history, null arguments, regime shifts, ties and empty series
  - `TestData`
    - `GemTestDataFactory` – helper for building synthetic return series for tests

- `tests/Strategies/Gem/Gem.Cli.Tests`  
  Tests focused on CLI-specific behavior:
  - `IO/GemCsvInputLoaderTests.cs`
    - verifies successful loading from valid CSV files
    - checks behavior when files are missing (`FileNotFoundException`)
    - checks behavior when numeric values are malformed (`FormatException`)
    - verifies that header-only files produce empty series
  - `ProgramTests.cs`
    - verifies exit code and error output when the sample data directory is missing
    - verifies successful run, exit code and console output when the sample data directory is present

---

### Configuration

- `config/gem/gem.cli.sample.json`  
  Sample GEM CLI configuration file. It documents how a future configuration-driven run will look:

    {
      "dataDirectory": "data/gem/sample",
      "usEquityFile": "us-equity.csv",
      "exUsEquityFile": "exus-equity.csv",
      "safeAssetFile": "safe-asset.csv",
      "outputSignalsFile": "dist/gem/signals.json",
      "lookbackMonths": 12
    }

  In Phase 2 this file is only a reference; it is not yet wired into the CLI.

- `config/gem/gem.cli.json`  
  User-specific CLI configuration file (planned for later phases).  
  This file is ignored by Git and is intended to hold local paths and preferences.

---

### Data folders

- `data/gem/sample/`  
  Synthetic, versioned sample data used for local runs and tests:
  - `us-equity.csv`
  - `exus-equity.csv`
  - `safe-asset.csv`

  Each file has the following structure:

    Year,Month,Return
    2025,1,0.02
    2025,2,0.03
    ...

  Semantics:
  - one row per month
  - `Return` is the monthly rate (e.g. 0.02 = 2%)

- `data/gem/raw/`  
  Reserved for real market data (e.g. exported or downloaded from external sources).  
  This directory is ignored by Git so that real data never accidentally enters version control.

---

### Output folder

- `dist/gem/`  
  Output directory for GEM-related artifacts:
  - `.gitkeep` – ensures the directory structure is tracked in Git
  - `signals.json` – planned output file for GEM signals

  The directory itself is tracked, but generated files under `dist/` are excluded from version control.  
  In Phase 2 the CLI prints signals only to the console; writing `signals.json` will be introduced in a later phase.

---

## Current status

The project is currently at **Phase 2**:

- Solution, projects and shared build configuration are in place.
- GEM domain model and engine are fully implemented and covered with unit tests.
- CSV-based input pipeline is implemented:
  - sample CSV files are provided under `data/gem/sample`
  - `GemCsvInputLoader` converts them into `GemInputData`
- The console application can perform a complete sample run:
  - load sample CSV data
  - run GEM with a configurable lookback window (3 months in the default Phase 2 setup)
  - display a summary and the generated monthly signals

Future phases will introduce:

- configuration-driven CLI (using `gem.cli.json`)
- writing signals to `dist/gem/signals.json`
- a static frontend consuming `signals.json`
- CI/CD with GitHub Actions and publishing via GitHub Pages

---

## Building and testing

From the repository root:

- Build the solution:

    dotnet build

- Run all tests:

    dotnet test

---

## Running a sample GEM execution

From the repository root:

1. Ensure the sample data files exist under `data/gem/sample`:
   - `us-equity.csv`
   - `exus-equity.csv`
   - `safe-asset.csv`

2. Run the GEM CLI:

    dotnet run --project src/Strategies/Gem/Gem.Cli/Gem.Cli.csproj

The application:

- resolves the sample data directory relative to the current working directory
- loads monthly returns from the three CSV files
- applies the GEM engine with a configured lookback window (3 months in the current Phase 2 sample)
- prints the number of generated signals and a line per month in the form:

    YYYY-MM: Position

For example:

    2025-03: UsEquity
    2025-04: SafeAsset
    2025-05: SafeAsset
    2025-06: SafeAsset

If you change the lookback window in `Program.cs` (for example back to `GemParameters.Default`, which uses 12 months), the generated signals and the example above will naturally change to reflect the new configuration.

As the project evolves, configuration, output paths and execution modes will be driven by `gem.cli.json` and higher-level runners rather than hard-coded paths in the `Program` entry point.

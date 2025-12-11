# StrategyNotifier

StrategyNotifier is a .NET-based tool for running and notifying about quantitative investment strategies.
The first implemented strategy is **GEM (Global Equities Momentum)**.

## Technology stack

- .NET 10 (SDK)
- C#
- xUnit for unit tests
- Visual Studio 2026
- Target deployment (planned for later phases):
  - GitHub Actions
  - GitHub Pages

## Solution structure

- `src/Strategies/Gem/Gem.Domain`  
  GEM domain model and core engine:
  - `Core` – fundamental value objects and series:
    - `YearMonth` – immutable representation of a year-month period
    - `MonthlyReturn` – monthly return for a given period
    - `AssetKind` – US equity, ex-US equity, safe asset
    - `AssetReturnSeries` – ordered, validated series of monthly returns
  - `Model` – strategy-specific data contracts:
    - `GemParameters` – GEM configuration (lookback window)
    - `GemInputData` – input container for all three asset series
    - `GemSignal` – monthly allocation decision for a given period
  - `Engine` – `GemEngine` implementing relative and absolute momentum logic
  - `Exceptions` – `DomainValidationException` for domain-level validation failures

- `src/Strategies/Gem/Gem.Cli`  
  Console application entry point for running GEM-related commands.  
  Currently a placeholder that will be extended in later phases to:
  - load configuration and input data,
  - invoke the GEM engine,
  - persist generated signals (e.g., to `signals.json`).

- `tests/Strategies/Gem/Gem.Domain.Tests`  
  Unit tests for the GEM domain and engine:
  - `Core` tests for `YearMonth`, `MonthlyReturn` and `AssetReturnSeries`
  - `Model` tests for `GemParameters` and `GemInputData`
  - `Engine` tests for `GemEngine` basic scenarios and edge cases
  - `TestData` helpers (`GemTestDataFactory`) for building synthetic return series

## Current status (Phase 1)

- Solution skeleton created
- Shared build configuration enforced via `Directory.Build.props`
- GEM domain model implemented:
  - value objects for periods and monthly returns
  - typed asset kinds and validated return series
  - domain model for GEM parameters, input data and signals
- GEM engine implemented:
  - identifies common periods across all three assets
  - builds rolling lookback windows per asset
  - selects the relative winner between US and ex-US equities
  - compares the winner momentum against the safe asset (absolute momentum)
  - produces a sequence of monthly allocation decisions
- Domain tests implemented with xUnit for:
  - YearMonth construction, comparison and month arithmetic
  - AssetReturnSeries sorting, duplicate detection and lookback windows
  - GemParameters validation
  - GemInputData invariants and null handling
  - GemEngine basic scenarios (US, ex-US, safe asset always winning)
  - GemEngine edge cases (insufficient history, null arguments, regime shifts, ties, empty series)
- CLI project present with a minimal placeholder entry point

## Prerequisites

- .NET 10 SDK installed
- Visual Studio 2026 (or `dotnet` CLI)

## How to build

From the repository root:

```bash
dotnet build
dotnet test
dotnet run --project src/Strategies/Gem/Gem.Cli/Gem.Cli.csproj
```

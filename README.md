# StrategyNotifier

StrategyNotifier is a .NET-based tool for running and notifying about quantitative investment strategies.
The first implemented strategy will be **GEM (Global Equities Momentum)**.

## Technology stack

- .NET 10 (SDK)
- C#
- xUnit for unit tests
- Visual Studio 2026
- Target deployment (planned for later phases):
  - GitHub Actions
  - GitHub Pages

## Solution structure (Phase 0)

- `src/Strategies/Gem/Gem.Domain` – domain model for the GEM strategy (empty in Phase 0, contains only a technical marker type)
- `src/Strategies/Gem/Gem.Cli` – console application entry point for running GEM-related commands
- `tests/Strategies/Gem/Gem.Domain.Tests` – unit tests for the GEM domain and related behavior

## Current status (Phase 0)

- Solution skeleton created
- Basic CLI entry point with a placeholder message
- GEM domain project created (no business logic yet)
- Test project configured with xUnit and a smoke test to verify the test setup

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

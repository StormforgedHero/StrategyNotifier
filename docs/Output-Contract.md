# Output Contract

## Location and format
- Path: `outputPath` from config (default: `dist/gem/signals.json`).
- Encoding: UTF-8 without BOM, indented JSON, with a trailing newline.
- Ordering: signals are written newest-first (descending by `date`/as-of date).
- The contract applies to each generated signals file (one per config/profile). In batch/profile runs, a manifest `dist/gem/profiles.json` is also produced for the frontend; the manifest is not part of the signals contract.

## Fields (high level)
- `date` (`yyyy-MM-dd`): as-of date used for momentum calculations.
- `windowMonths` (int): lookback window from configuration.
- `isRiskOn` (bool): whether the risk-on set won the allocation.
- `absoluteReturn` (decimal): return of the winning risk-on instrument over `windowMonths`.
- `relativeRank` (int): 1-based rank of the winning risk-on instrument.
- `comment` (string): never null; empty string when absent.
- `allocations` (array):
  - `ticker` (string)
  - `name` (string)
  - `weight` (decimal in [0,1])

## Ordering guarantees
- Signals sorted newest-first before serialization.
- Allocations sorted by weight descending, then ticker ascending for stability.

## Determinism
- Golden and encoding tests lock ordering, encoding (UTF-8 without BOM + trailing newline), camelCase property names, and culture-invariant formatting.

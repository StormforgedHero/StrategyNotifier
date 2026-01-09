# Data & Update Flow

## Offline-first principle
- Calculations always read from the store; no network is needed to run or test.
- Updates are optional and isolated to the cache (with optional promotion to the store).

## Directories
- `storeDirectory`: source of truth for calculations.
- `cacheDirectory`: download staging plus per-symbol attempt metadata.

## Update modes
- `--no-update`: never fetch (preferred for CI/tests).
- `--force-update`: always fetch; for manual use only, not CI.
- No flags: follow `update.autoUpdateEnabled`; if enabled, attempts are throttled by `minMinutesBetweenAttempts`.

## Staleness
- `maxAgeDays` compares the last available price date in the store to `DateTime.UtcNow.Date`.
- Missing or too-old data is considered stale and may trigger an update (unless `--no-update`).

## Attempt cooldown
- Per-instrument attempt timestamps are stored in the cache as `<sourceSymbol>.last_attempt` (UTC, `O` format).
- If the elapsed time since the last attempt is less than `minMinutesBetweenAttempts`, the updater skips the fetch (unless forced).

## Promotion
- Downloads land in the cache as `<sourceSymbol>.csv`.
- When `saveUpdatedDataToStore=true`, a successful fetch promotes the cached CSV to the store (atomic write).
- When `saveUpdatedDataToStore=false`, the store is never touched; calculations still read the store even if the cache is fresher.

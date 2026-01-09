# Configuration

## Config schema (final)
- Location: `config/gem/gem.config.json`, resolved relative to the current working directory. Unknown fields (including casing variants) fail fast before any work starts.
- `windowMonths` (int, >0, default 12): momentum lookback window in months.
- `rankingMode` (`Top1` or `Top2`, default `Top1`): allocation rule for risk-on instruments.
- `instruments` (object, required):
  - `usEquity`, `exUsEquity`, `safeAsset` are all required.
  - Each requires `ticker`, `name`, `sourceSymbol` (all non-empty).
  - `ticker` and `sourceSymbol` must be unique across all instruments (case-insensitive).
- `storeDirectory` (string, required): authoritative price files used for calculations.
- `cacheDirectory` (string, required): download staging and attempt metadata.
- `outputPath` (string, required): target for `signals.json`.
- `update` (object, optional; defaults apply if omitted):
  - `autoUpdateEnabled` (bool, default `false`)
  - `maxAgeDays` (int, >=0, default `2`)
  - `minMinutesBetweenAttempts` (int, >=0, default `30`; whole minutes only)
  - `saveUpdatedDataToStore` (bool, default `true`)

## File naming convention
- Price CSV files in both store and cache are named `<sourceSymbol>.csv`.
- `sourceSymbol` is also the Stooq download key; lowercase is recommended (e.g., `voo.us`).

## Example (US portfolio)
```
{
  "windowMonths": 12,
  "rankingMode": "Top1",
  "instruments": {
    "usEquity":   { "ticker": "VOO.US", "name": "US Equity",    "sourceSymbol": "voo.us" },
    "exUsEquity": { "ticker": "VEU.US", "name": "Ex-US Equity", "sourceSymbol": "veu.us" },
    "safeAsset":  { "ticker": "AGG.US", "name": "US Bonds",     "sourceSymbol": "agg.us" }
  },
  "storeDirectory": "data/gem/sample",
  "cacheDirectory": "data/gem/cache",
  "outputPath": "dist/gem/signals.json",
  "update": {
    "autoUpdateEnabled": false,
    "maxAgeDays": 2,
    "minMinutesBetweenAttempts": 30,
    "saveUpdatedDataToStore": true
  }
}
```

## Validation rules
- Unknown or misspelled fields cause startup failure.
- `storeDirectory`, `cacheDirectory`, and `outputPath` are required; the store must already exist, cache/output directories are created if needed.
- Numeric fields must be non-negative; `minMinutesBetweenAttempts` must be a whole number of minutes.

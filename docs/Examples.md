# Examples

Adjust `instruments.*` in `config/gem/gem.config.json` and place matching CSV files (`<sourceSymbol>.csv`) in your `storeDirectory`. Keep `sourceSymbol` lowercase to match the CSV filename prefix exactly. All instruments in a run should use the same pricing currency; mixing USD/EUR/PLN without FX adjustment will distort momentum. Sample CSVs in `data/gem/sample` cover the US, EU (set B), and PL portfolios listed below.

How to run:
- All profiles (default batch): `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update`
- Single profile (built-in ids): `--profile=us` | `--profile=eu` | `--profile=pl`
- Single explicit config: `--config=config/gem/gem.config.json`
- If data is already in the store, use `--no-update`. If missing data and working offline, run once with `--force-update` (manual only) plus `update.saveUpdatedDataToStore=true` to promote downloads. Never use `--force-update` in tests or CI.

## 1) US (repo sample, Stooq-verified)
- `usEquity`: `ticker=VOO.US`, `sourceSymbol=voo.us`
- `exUsEquity`: `ticker=VEU.US`, `sourceSymbol=veu.us`
- `safeAsset`: `ticker=AGG.US`, `sourceSymbol=agg.us`
- Required store files: `voo.us.csv`, `veu.us.csv`, `agg.us.csv`

## 2) EU (set A)
- `usEquity`: `ticker=CSPX.UK`, `sourceSymbol=cspx.uk`
- `exUsEquity`: `ticker=EXUS.DE`, `sourceSymbol=exus.de`
- `safeAsset`: `ticker=IUAE.UK`, `sourceSymbol=iuae.uk`
- Required store files: `cspx.uk.csv`, `exus.de.csv`, `iuae.uk.csv`

## 3) EU (set B)
- `usEquity`: `ticker=SPPW.DE`, `sourceSymbol=sppw.de`
- `exUsEquity`: `ticker=IS3N.DE`, `sourceSymbol=is3n.de`
- `safeAsset`: `ticker=EUNA.DE`, `sourceSymbol=euna.de`
- Required store files: `sppw.de.csv`, `is3n.de.csv`, `euna.de.csv`

## 4) PL (GPW)
- `usEquity`: `ticker=ETFBW20TR.PL`, `sourceSymbol=etfbw20tr.pl`
- `exUsEquity`: `ticker=ETFBM40TR.PL`, `sourceSymbol=etfbm40tr.pl`
- `safeAsset`: `ticker=ETFBCASH.PL`, `sourceSymbol=etfbcash.pl`
- Required store files: `etfbw20tr.pl.csv`, `etfbm40tr.pl.csv`, `etfbcash.pl.csv`

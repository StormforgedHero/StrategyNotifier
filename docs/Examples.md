# Examples

Adjust `instruments.*` in `config/gem/gem.config.json` and place matching CSV files (`<sourceSymbol>.csv`) in your `storeDirectory`. All instruments in a run should use the same pricing currency; mixing USD/EUR/PLN without FX adjustment will distort momentum. Sample CSVs in `data/gem/sample` cover the US, EU (set B), and PL portfolios listed below.

For each portfolio below:
- If you already have the CSVs in the store, run with `--no-update`.
- If you do not have data yet, run once with `--force-update` (manual use only) and `update.saveUpdatedDataToStore=true` to promote downloads into the store. Do not use `--force-update` in tests or CI.

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
- `safeAsset`: `ticker=ETFBNDXPL.PL`, `sourceSymbol=etfbndxpl.pl`
- Required store files: `etfbw20tr.pl.csv`, `etfbm40tr.pl.csv`, `etfbndxpl.pl.csv`

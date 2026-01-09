# Algorithm and Assumptions

## Momentum definition
- Uses daily close prices (`Date,Close`).
- Conceptually: return = `close(asOfDate) / close(asOfDate - windowMonths) - 1`.
- `asOfDate` uses the latest available price on or before the target date; the start price uses the latest available price on or before `asOfDate - windowMonths`.
- Missing start/end prices or zero start price cause the month to be skipped.

## Ranking
- Risk-on set = `instruments.usEquity` + `instruments.exUsEquity` (and any additional configured risk-on entries).
- Compute momentum for each risk-on instrument, rank descending by return.
- `Top1`: allocate 100% to the top-ranked risk-on instrument.
- `Top2`: split 50/50 between the top two ranked risk-on instruments.

## Absolute threshold
- The absolute momentum gate is always enabled with threshold `0.0`.
- Portfolio stays risk-on only if the leader's return is greater than the threshold; otherwise, it moves fully to the safe asset.

## As-of date selection
- Monthly cadence: iterate month-end checkpoints across the common price history window.
- For each month-end, pick the latest date where all instruments have data on or before that month-end; if none exists, skip the month.
- Output `date` is this common as-of date (not necessarily the calendar month-end).

## Currency assumption
- Instruments should be priced in the same currency for clean comparisons; mixing currencies implicitly bakes FX movements into momentum.

## Why newest-first
- Signals are sorted newest-first to make consumption simpler and to match the contract locked by golden tests.

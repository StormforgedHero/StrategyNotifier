using Gem.Domain.Exceptions;

namespace Gem.Domain.Core
{
    public sealed class AssetReturnSeries
    {
        private readonly List<MonthlyReturn> _returns;

        public AssetKind AssetKind { get; }

        public IReadOnlyList<MonthlyReturn> Returns => _returns;

        public AssetReturnSeries(AssetKind assetKind, IEnumerable<MonthlyReturn> returns)
        {
            ArgumentNullException.ThrowIfNull(returns);

            AssetKind = assetKind;

            var items = returns.ToList();

            if (items.Count == 0)
            {
                _returns = new List<MonthlyReturn>();
                return;
            }

            items.Sort((left, right) => left.Period.CompareTo(right.Period));

            for (var index = 1; index < items.Count; index++)
            {
                if (items[index].Period == items[index - 1].Period)
                {
                    throw new DomainValidationException(
                        $"Duplicate period {items[index].Period.Year}-{items[index].Period.Month:00} in return series.");
                }
            }

            _returns = items;
        }

        public IReadOnlyList<MonthlyReturn> GetLookbackWindow(YearMonth endPeriod, int lookbackMonths)
        {
            if (lookbackMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lookbackMonths),
                    lookbackMonths,
                    "Lookback months must be greater than zero.");
            }

            if (_returns.Count == 0)
            {
                throw new DomainValidationException("Return series is empty.");
            }

            int endIndex = FindIndexOfPeriod(endPeriod);

            if (endIndex < 0)
            {
                throw new DomainValidationException(
                    $"Return series does not contain end period {endPeriod.Year}-{endPeriod.Month:00}.");
            }

            int startIndex = endIndex - lookbackMonths + 1;

            if (startIndex < 0)
            {
                throw new InsufficientHistoryException(
                    "Not enough data points to build the requested lookback window.");
            }

            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                YearMonth expected = _returns[i - 1].Period.AddMonths(1);

                if (_returns[i].Period != expected)
                {
                    throw new NonConsecutivePeriodsException(
                        "Return series contains a gap inside the requested lookback window.");
                }
            }

            var result = new MonthlyReturn[lookbackMonths];

            for (int i = 0; i < lookbackMonths; i++)
            {
                result[i] = _returns[startIndex + i];
            }

            return result;
        }

        private int FindIndexOfPeriod(YearMonth period)
        {
            for (int i = 0; i < _returns.Count; i++)
            {
                int comparison = _returns[i].Period.CompareTo(period);

                if (comparison == 0)
                {
                    return i;
                }

                if (comparison > 0)
                {
                    break;
                }
            }

            return -1;
        }
    }
}

using Gem.Domain.Core;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;

namespace Gem.Domain.Engine
{
    /// <summary>
    /// Provides methods for generating GEM strategy signals based on
    /// monthly return series for US equity, ex-US equity and a safe asset.
    /// </summary>
    public sealed class GemEngine
    {
        /// <summary>
        /// Generates GEM signals for all periods where sufficient history
        /// is available for all three assets.
        /// </summary>
        /// <param name="input">Input data containing return series for all assets.</param>
        /// <param name="parameters">GEM strategy parameters.</param>
        /// <returns>
        /// A list of <see cref="GemSignal"/> instances ordered by period in ascending order.
        /// If there is not enough data to build any lookback window, an empty list is returned.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="input"/> or <paramref name="parameters"/> is <c>null</c>.
        /// </exception>
        public IReadOnlyList<GemSignal> GenerateSignals(GemInputData input, GemParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(parameters);

            if (input.UsEquity.Returns.Count == 0
                || input.ExUsEquity.Returns.Count == 0
                || input.SafeAsset.Returns.Count == 0)
            {
                return Array.Empty<GemSignal>();
            }

            int lookbackMonths = parameters.LookbackMonths;

            YearMonth[] candidatePeriods = GetCandidatePeriods(input);

            var signals = new List<GemSignal>();

            foreach (YearMonth period in candidatePeriods)
            {
                IReadOnlyList<MonthlyReturn> usWindow;
                IReadOnlyList<MonthlyReturn> exUsWindow;
                IReadOnlyList<MonthlyReturn> safeWindow;

                try
                {
                    usWindow = input.UsEquity.GetLookbackWindow(period, lookbackMonths);
                    exUsWindow = input.ExUsEquity.GetLookbackWindow(period, lookbackMonths);
                    safeWindow = input.SafeAsset.GetLookbackWindow(period, lookbackMonths);
                }
                catch (InsufficientHistoryException)
                {
                    // Not enough history to build the lookback window for at least one series
                    // for this period. Such periods are skipped.
                    continue;
                }
                catch (NonConsecutivePeriodsException)
                {
                    // The lookback window contains a gap (non-consecutive periods).
                    // Such periods are skipped to allow generating signals where possible.
                    continue;
                }

                decimal usMomentum = SumRates(usWindow);
                decimal exUsMomentum = SumRates(exUsWindow);
                decimal safeMomentum = SumRates(safeWindow);

                AssetKind relativeWinnerKind = AssetKind.UsEquity;
                decimal relativeWinnerMomentum = usMomentum;

                if (exUsMomentum > usMomentum)
                {
                    relativeWinnerKind = AssetKind.ExUsEquity;
                    relativeWinnerMomentum = exUsMomentum;
                }

                AssetKind position = relativeWinnerMomentum > safeMomentum
                    ? relativeWinnerKind
                    : AssetKind.SafeAsset;

                signals.Add(new GemSignal(period, position));
            }

            return signals;
        }

        private static YearMonth[] GetCandidatePeriods(GemInputData input)
        {
            IEnumerable<YearMonth> usPeriods = input.UsEquity.Returns.Select(r => r.Period);
            IEnumerable<YearMonth> exUsPeriods = input.ExUsEquity.Returns.Select(r => r.Period);
            IEnumerable<YearMonth> safePeriods = input.SafeAsset.Returns.Select(r => r.Period);

            YearMonth[] candidatePeriods = usPeriods
                .Intersect(exUsPeriods)
                .Intersect(safePeriods)
                .Distinct()
                .OrderBy(period => period)
                .ToArray();

            return candidatePeriods;
        }

        private static decimal SumRates(IEnumerable<MonthlyReturn> window)
        {
            decimal sum = 0m;

            foreach (MonthlyReturn monthlyReturn in window)
            {
                sum += monthlyReturn.Rate;
            }

            return sum;
        }
    }
}

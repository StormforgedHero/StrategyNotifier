using System.Globalization;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Domain.Engine
{
    public sealed class GemEngine
    {
        private readonly IPriceSeriesRepository _repository;

        public GemEngine(IPriceSeriesRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public Signal GenerateSignal(DateOnly asOfDate, PortfolioConfiguration portfolio)
        {
            ArgumentNullException.ThrowIfNull(portfolio);

            int windowMonths = portfolio.Momentum.WindowMonths;

            var riskOnResults = new List<(Instrument Instrument, decimal Return)>();

            foreach (Instrument instrument in portfolio.RiskOnInstruments)
            {
                IReadOnlyList<PricePoint> series = _repository.GetSeries(instrument);
                decimal momentum = CalculateReturn(series, asOfDate, windowMonths);
                riskOnResults.Add((instrument, momentum));
            }

            (Instrument Instrument, decimal Return) leader = riskOnResults
                .OrderByDescending(item => item.Return)
                .First();

            bool isRiskOn = !portfolio.Momentum.UseAbsoluteMomentum
                || leader.Return > portfolio.Momentum.AbsoluteThreshold;

            IReadOnlyList<Allocation> allocations = isRiskOn
                ? BuildRiskOnAllocations(riskOnResults, portfolio.Momentum)
                : new[] { new Allocation(portfolio.RiskOffInstrument, 1m) };

            int relativeRank = GetRelativeRank(riskOnResults, leader.Instrument);
            string comment = BuildComment(leader, isRiskOn);

            return new Signal(
                date: asOfDate,
                windowMonths: windowMonths,
                isRiskOn: isRiskOn,
                allocations: allocations,
                absoluteReturn: leader.Return,
                relativeRank: relativeRank,
                comment: comment);
        }

        private static decimal CalculateReturn(
            IReadOnlyList<PricePoint> series,
            DateOnly asOfDate,
            int windowMonths)
        {
            if (series.Count == 0)
            {
                throw new DomainValidationException("Price series is empty.");
            }

            PricePoint? end = series.LastOrDefault(p => p.Date <= asOfDate);

            if (end is null)
            {
                throw new DomainValidationException($"No price data on or before {asOfDate:yyyy-MM-dd}.");
            }

            DateOnly startCandidateDate = asOfDate.AddMonths(-windowMonths);
            PricePoint? start = series.LastOrDefault(p => p.Date <= startCandidateDate);

            if (start is null)
            {
                throw new DomainValidationException(
                    $"Insufficient history to build a {windowMonths}-month window ending at {asOfDate:yyyy-MM-dd}.");
            }

            if (start.Close == 0m)
            {
                throw new DomainValidationException("Start price is zero, cannot compute return.");
            }

            return end.Close / start.Close - 1m;
        }

        private static IReadOnlyList<Allocation> BuildRiskOnAllocations(
            IReadOnlyList<(Instrument Instrument, decimal Return)> ranked,
            MomentumParameters parameters)
        {
            var sorted = ranked
                .OrderByDescending(item => item.Return)
                .ToList();

            if (parameters.RankingMode == RankingMode.Top2 && sorted.Count >= 2)
            {
                return new[]
                {
                    new Allocation(sorted[0].Instrument, 0.5m),
                    new Allocation(sorted[1].Instrument, 0.5m)
                };
            }

            return new[] { new Allocation(sorted[0].Instrument, 1m) };
        }

        private static string BuildComment((Instrument Instrument, decimal Return) leader, bool isRiskOn)
        {
            string direction = isRiskOn ? "risk-on" : "risk-off";
            string formattedReturn = leader.Return.ToString("P2", CultureInfo.InvariantCulture);
            return $"{leader.Instrument.Ticker} return {formattedReturn} => {direction}";
        }

        private static int GetRelativeRank(
            IReadOnlyList<(Instrument Instrument, decimal Return)> ranked,
            Instrument leader)
        {
            var sorted = ranked
                .OrderByDescending(item => item.Return)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (ReferenceEquals(sorted[i].Instrument, leader)
                    || string.Equals(sorted[i].Instrument.Ticker, leader.Ticker, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return 1;
        }
    }
}

using System.Globalization;
using System.Text.Json;
using Gem.Cli.Configuration;
using Gem.Cli.Contracts;
using Gem.Cli.Pricing;
using Gem.Cli.Utilities;
using Gem.Domain.Engine;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Execution
{
    public sealed class GemRunner
    {
        private readonly GemCliConfiguration _configuration;
        private readonly IPriceDataUpdater _updater;
        private readonly IPriceDataProvider _localProvider;

        public GemRunner(
            GemCliConfiguration configuration,
            IPriceDataUpdater? updater = null,
            IPriceDataProvider? localProvider = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _localProvider = localProvider ?? new LocalCsvPriceDataProvider();
            _updater = updater ?? CreateDefaultUpdater(configuration);
        }

        public IReadOnlyList<Signal> Run(bool skipUpdate, bool forceUpdate)
        {
            if (_configuration.Update.Enabled && !skipUpdate)
            {
                _updater.UpdateAsync(GetAllInstruments(), forceUpdate, CancellationToken.None).GetAwaiter().GetResult();
            }

            var repository = new FilePriceSeriesRepository(_configuration.DataDirectory, _localProvider);
            var engine = new GemEngine(repository);

            IReadOnlyList<Signal> signals = GenerateSignalHistory(engine, repository, _configuration.Portfolio);

            WriteSignals(signals);

            return signals;
        }

        private static IPriceDataUpdater CreateDefaultUpdater(GemCliConfiguration configuration)
        {
            var httpProvider = new StooqCsvHttpDataProvider();
            var freshness = TimeSpan.FromDays(configuration.Update.FreshnessDays);
            var minDelay = TimeSpan.FromSeconds(configuration.Update.MinDelaySeconds);
            return new PriceDataUpdater(httpProvider, configuration.DataDirectory, freshness, minDelay);
        }

        private IEnumerable<Instrument> GetAllInstruments()
        {
            foreach (Instrument instrument in _configuration.Portfolio.RiskOnInstruments)
            {
                yield return instrument;
            }

            yield return _configuration.Portfolio.RiskOffInstrument;
        }

        private static IReadOnlyList<Signal> GenerateSignalHistory(
            GemEngine engine,
            IPriceSeriesRepository repository,
            PortfolioConfiguration portfolio)
        {
            var allSeries = new List<IReadOnlyList<PricePoint>>();

            foreach (Instrument instrument in portfolio.RiskOnInstruments)
            {
                allSeries.Add(repository.GetSeries(instrument));
            }

            allSeries.Add(repository.GetSeries(portfolio.RiskOffInstrument));

            if (allSeries.Count == 0)
            {
                return Array.Empty<Signal>();
            }

            DateOnly? earliestCommonStart = allSeries
                .Select(series => series.FirstOrDefault()?.Date)
                .Where(date => date.HasValue)
                .Max();

            DateOnly? latestCommonEnd = allSeries
                .Select(series => series.LastOrDefault()?.Date)
                .Where(date => date.HasValue)
                .Min();

            if (earliestCommonStart is null || latestCommonEnd is null)
            {
                return Array.Empty<Signal>();
            }

            DateOnly firstAsOf = EndOfMonth(earliestCommonStart.Value.AddMonths(portfolio.Momentum.WindowMonths));
            DateOnly lastMonth = EndOfMonth(latestCommonEnd.Value);

            var signals = new List<Signal>();

            for (DateOnly current = firstAsOf;
                 current <= lastMonth;
                 current = NextMonthEnd(current))
            {
                DateOnly? asOfDate = FindCommonAsOfDate(current, allSeries);

                if (asOfDate is null)
                {
                    continue;
                }

                try
                {
                    signals.Add(engine.GenerateSignal(asOfDate.Value, portfolio));
                }
                catch (DomainValidationException)
                {
                    // Skip months without enough history for all inputs.
                }
            }

            return signals
                .OrderBy(signal => signal.Date)
                .ToList();
        }

        private static DateOnly NextMonthEnd(DateOnly date)
        {
            DateOnly firstOfMonth = new(date.Year, date.Month, 1);
            DateOnly nextMonth = firstOfMonth.AddMonths(1);
            return EndOfMonth(nextMonth);
        }

        private static DateOnly EndOfMonth(DateOnly date)
        {
            int days = DateTime.DaysInMonth(date.Year, date.Month);
            return new DateOnly(date.Year, date.Month, days);
        }

        private static DateOnly? FindCommonAsOfDate(
            DateOnly monthEnd,
            IReadOnlyList<IReadOnlyList<PricePoint>> seriesList)
        {
            if (seriesList.Count == 0)
            {
                return null;
            }

            var dateSets = seriesList
                .Select(series => series.Where(p => p.Date <= monthEnd).Select(p => p.Date).ToHashSet())
                .ToList();

            if (dateSets.Any(set => set.Count == 0))
            {
                return null;
            }

            HashSet<DateOnly> intersection = dateSets[0];

            foreach (HashSet<DateOnly> set in dateSets.Skip(1))
            {
                intersection.IntersectWith(set);

                if (intersection.Count == 0)
                {
                    return null;
                }
            }

            return intersection.Max();
        }

        private void WriteSignals(IReadOnlyList<Signal> signals)
        {
            var outputs = signals
                .Select(MapToOutput)
                .ToList();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(outputs, options);

            string outputPath = _configuration.OutputSignalsFile;

            try
            {
                string? directory = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, json, FileEncodings.Utf8NoBom);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new GemOutputWriteException(outputPath, ex);
            }
        }

        private static SignalOutput MapToOutput(Signal signal)
        {
            var allocations = signal.Allocations
                .Select(a => new AllocationOutput
                {
                    Ticker = a.Instrument.Ticker,
                    Name = a.Instrument.Name,
                    Weight = a.Weight
                })
                .ToArray();

            return new SignalOutput
            {
                Date = signal.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                WindowMonths = signal.WindowMonths,
                IsRiskOn = signal.IsRiskOn,
                Allocations = allocations,
                AbsoluteReturn = signal.AbsoluteReturn,
                RelativeRank = signal.RelativeRank,
                Comment = signal.Comment
            };
        }
    }
}

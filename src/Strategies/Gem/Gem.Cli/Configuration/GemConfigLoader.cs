using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;
using Gem.Domain.Model;

namespace Gem.Cli.Configuration
{
    public sealed class GemConfigLoader
    {
        private readonly string _path;

        public GemConfigLoader(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Configuration path must be provided.", nameof(path));
            }

            _path = path;
        }

        public GemCliConfiguration Load()
        {
            if (!File.Exists(_path))
            {
                throw new FileNotFoundException(
                    $"Configuration file not found at '{_path}'.",
                    _path);
            }

            string json = File.ReadAllText(_path, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Configuration file is empty.");
            }

            GemConfigFile? config;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                config = JsonSerializer.Deserialize<GemConfigFile>(json, options);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Configuration file contains invalid JSON.", ex);
            }

            if (config is null)
            {
                throw new InvalidOperationException("Configuration file is empty.");
            }

            return BuildConfiguration(config);
        }

        private static GemCliConfiguration BuildConfiguration(GemConfigFile config)
        {
            if (config.Instruments is null)
            {
                throw new InvalidOperationException("Instruments configuration is required.");
            }

            IReadOnlyList<Instrument> riskOnInstruments = BuildRiskOnInstruments(config.Instruments);

            if (config.Instruments.SafeAsset is null)
            {
                throw new InvalidOperationException("Safe asset configuration is required.");
            }

            int window = config.WindowMonths ?? 12;
            RankingMode ranking = ParseRanking(config.RankingMode);
            var momentum = new MomentumParameters(window, ranking, useAbsoluteMomentum: true, absoluteThreshold: 0m);

            Instrument safe = BuildInstrument(config.Instruments.SafeAsset, "Safe Asset");

            ValidateRiskOnCount(riskOnInstruments, ranking);
            ValidateUniqueSymbols(riskOnInstruments.Concat(new[] { safe }));

            var portfolio = new PortfolioConfiguration(riskOnInstruments, safe, momentum);

            UpdateSettings update = BuildUpdateSettings(config.Update);

            string dataDirectory = string.IsNullOrWhiteSpace(config.DataDirectory)
                ? "data/gem/sample"
                : config.DataDirectory!;

            string outputSignalsFile = string.IsNullOrWhiteSpace(config.OutputPath)
                ? "dist/gem/signals.json"
                : config.OutputPath!;

            return new GemCliConfiguration(
                portfolio,
                update,
                dataDirectory,
                outputSignalsFile);
        }

        private static RankingMode ParseRanking(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && Enum.TryParse<RankingMode>(value, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return RankingMode.Top1;
        }

        private static UpdateSettings BuildUpdateSettings(UpdateConfig? update)
        {
            var settings = new UpdateSettings
            {
                Enabled = update?.EnabledByDefault ?? true,
                FreshnessDays = update?.FreshnessDays ?? 2,
                MinDelaySeconds = update?.MinHoursBetweenUpdates.HasValue == true
                    ? (int)(update!.MinHoursBetweenUpdates.Value * 3600)
                    : 1
            };

            settings.EnsureDefaults();
            return settings;
        }

        private static IReadOnlyList<Instrument> BuildRiskOnInstruments(InstrumentsConfig instruments)
        {
            if (instruments.RiskOn is { Count: > 0 })
            {
                if (instruments.RiskOn.Any(item => item is null))
                {
                    throw new InvalidOperationException("Instruments.riskOn cannot contain null entries.");
                }

                return instruments.RiskOn
                    .Select((config, index) => BuildInstrument(config, $"Risk-On {index + 1}"))
                    .ToList();
            }

            if (instruments.UsEquity is null || instruments.ExUsEquity is null)
            {
                throw new InvalidOperationException("Risk-on instruments must be provided via 'riskOn' or both 'usEquity' and 'exUsEquity'.");
            }

            return new[]
            {
                BuildInstrument(instruments.UsEquity, "US Equity"),
                BuildInstrument(instruments.ExUsEquity, "Ex-US Equity")
            };
        }

        private static void ValidateRiskOnCount(IReadOnlyList<Instrument> riskOnInstruments, RankingMode ranking)
        {
            if (riskOnInstruments.Count == 0)
            {
                throw new InvalidOperationException("At least one risk-on instrument must be provided.");
            }

            if (ranking == RankingMode.Top2 && riskOnInstruments.Count < 2)
            {
                throw new InvalidOperationException("RankingMode=Top2 requires at least two risk-on instruments.");
            }
        }

        private static Instrument BuildInstrument(InstrumentConfig config, string fallbackName)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (string.IsNullOrWhiteSpace(config.Ticker))
            {
                throw new InvalidOperationException("Instrument ticker is required.");
            }

            if (config.SourceSymbol is not null && string.IsNullOrWhiteSpace(config.SourceSymbol))
            {
                throw new InvalidOperationException("Instrument sourceSymbol cannot be empty when provided.");
            }

            return new Instrument(
                config.Ticker,
                string.IsNullOrWhiteSpace(config.Name) ? fallbackName : config.Name!,
                config.SourceSymbol);
        }

        private static void ValidateUniqueSymbols(IEnumerable<Instrument> instruments)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Instrument instrument in instruments)
            {
                AddOrThrow(instrument.Ticker);

                if (!string.IsNullOrWhiteSpace(instrument.SourceSymbol)
                    && !string.Equals(instrument.SourceSymbol, instrument.Ticker, StringComparison.OrdinalIgnoreCase))
                {
                    AddOrThrow(instrument.SourceSymbol!);
                }
            }

            void AddOrThrow(string value)
            {
                string key = value.Trim();

                if (!seen.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate instrument identifier detected: '{key}'.");
                }
            }
        }
    }
}

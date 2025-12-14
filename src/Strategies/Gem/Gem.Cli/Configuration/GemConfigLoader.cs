using System.Text;
using System.Text.Json;
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

            if (config.Instruments.UsEquity is null
                || config.Instruments.ExUsEquity is null
                || config.Instruments.SafeAsset is null)
            {
                throw new InvalidOperationException("All instruments (usEquity, exUsEquity, safeAsset) must be provided.");
            }

            int window = config.WindowMonths ?? 12;
            RankingMode ranking = ParseRanking(config.RankingMode);
            var momentum = new MomentumParameters(window, ranking, useAbsoluteMomentum: true, absoluteThreshold: 0m);

            Instrument us = BuildInstrument(config.Instruments.UsEquity, "US Equity");
            Instrument exUs = BuildInstrument(config.Instruments.ExUsEquity, "Ex-US Equity");
            Instrument safe = BuildInstrument(config.Instruments.SafeAsset, "Safe Asset");

            ValidateUniqueSymbols(new[] { us, exUs, safe });

            var portfolio = new PortfolioConfiguration(new[] { us, exUs }, safe, momentum);

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

        private static Instrument BuildInstrument(InstrumentConfig config, string fallbackName)
        {
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

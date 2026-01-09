using Gem.Domain.Model;
using System.Text;
using System.Text.Json;

namespace Gem.Cli.Configuration
{
    public sealed class GemConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                ValidateKnownFields(document.RootElement);

                GemConfigFile? config = JsonSerializer.Deserialize<GemConfigFile>(json, JsonOptions);

                if (config is null)
                {
                    throw new InvalidOperationException("Configuration file is empty.");
                }

                return BuildConfiguration(config);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Configuration file contains invalid JSON.", ex);
            }
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

            Instrument safe = BuildInstrument(config.Instruments.SafeAsset);

            ValidateRiskOnCount(riskOnInstruments, ranking);
            ValidateUniqueSymbols(riskOnInstruments.Concat(new[] { safe }));

            var portfolio = new PortfolioConfiguration(riskOnInstruments, safe, momentum);

            UpdateSettings update = BuildUpdateSettings(config.Update);

            if (string.IsNullOrWhiteSpace(config.StoreDirectory))
            {
                throw new InvalidOperationException("storeDirectory is required.");
            }

            if (string.IsNullOrWhiteSpace(config.CacheDirectory))
            {
                throw new InvalidOperationException("cacheDirectory is required.");
            }

            if (string.IsNullOrWhiteSpace(config.OutputPath))
            {
                throw new InvalidOperationException("outputPath is required.");
            }

            string storeDirectory = config.StoreDirectory!;
            string cacheDirectory = config.CacheDirectory!;
            string outputSignalsFile = config.OutputPath!;

            return new GemCliConfiguration(
                portfolio,
                update,
                storeDirectory,
                cacheDirectory,
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
            bool autoUpdate = update?.AutoUpdateEnabled ?? false;
            int? maxAgeDays = update?.MaxAgeDays;
            int? minMinutesBetweenAttempts = update?.MinMinutesBetweenAttempts;

            var settings = new UpdateSettings
            {
                AutoUpdateEnabled = autoUpdate,
                MaxAgeDays = maxAgeDays ?? 2,
                MinMinutesBetweenAttempts = minMinutesBetweenAttempts ?? 30,
                SaveUpdatedDataToStore = update?.SaveUpdatedDataToStore ?? true
            };

            settings.Validate();
            return settings;
        }

        private static IReadOnlyList<Instrument> BuildRiskOnInstruments(InstrumentsConfig instruments)
        {
            if (instruments.UsEquity is null || instruments.ExUsEquity is null)
            {
                throw new InvalidOperationException("Risk-on instruments must include both 'usEquity' and 'exUsEquity'.");
            }

            return new[]
            {
                BuildInstrument(instruments.UsEquity),
                BuildInstrument(instruments.ExUsEquity)
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

        private static Instrument BuildInstrument(InstrumentConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (string.IsNullOrWhiteSpace(config.Ticker))
            {
                throw new InvalidOperationException("Instrument ticker is required.");
            }

            if (string.IsNullOrWhiteSpace(config.Name))
            {
                throw new InvalidOperationException("Instrument name is required.");
            }

            if (string.IsNullOrWhiteSpace(config.SourceSymbol))
            {
                throw new InvalidOperationException("Instrument sourceSymbol is required.");
            }

            return new Instrument(
                config.Ticker,
                config.Name!,
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

        private static void ValidateKnownFields(JsonElement root)
        {
            var topLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "windowMonths",
                "rankingMode",
                "instruments",
                "update",
                "storeDirectory",
                "cacheDirectory",
                "outputPath"
            };

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!topLevel.Contains(property.Name))
                {
                    throw new InvalidOperationException($"Configuration contains unsupported field '{property.Name}'.");
                }

                if (string.Equals(property.Name, "instruments", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateInstruments(property.Value);
                }
                else if (string.Equals(property.Name, "update", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateUpdate(property.Value);
                }
            }
        }

        private static void ValidateInstruments(JsonElement instrumentsElement)
        {
            if (instrumentsElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("instruments must be an object.");
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "usEquity",
                "exUsEquity",
                "safeAsset"
            };

            foreach (JsonProperty property in instrumentsElement.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    throw new InvalidOperationException($"Configuration contains unsupported field 'instruments.{property.Name}'.");
                }

                ValidateInstrument(property.Value, property.Name);
            }
        }

        private static void ValidateInstrument(JsonElement instrumentElement, string context)
        {
            if (instrumentElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Instrument '{context}' must be an object.");
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ticker",
                "name",
                "sourceSymbol"
            };

            foreach (JsonProperty property in instrumentElement.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    throw new InvalidOperationException($"Configuration contains unsupported field 'instruments.{context}.{property.Name}'.");
                }
            }
        }

        private static void ValidateUpdate(JsonElement updateElement)
        {
            if (updateElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("update must be an object.");
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "autoUpdateEnabled",
                "maxAgeDays",
                "minMinutesBetweenAttempts",
                "saveUpdatedDataToStore"
            };

            foreach (JsonProperty property in updateElement.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    throw new InvalidOperationException($"Configuration contains unsupported field 'update.{property.Name}'.");
                }

                if (string.Equals(property.Name, "minMinutesBetweenAttempts", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _))
                    {
                        throw new InvalidOperationException("minMinutesBetweenAttempts must be specified in whole minutes (integer).");
                    }
                }
            }
        }
    }
}

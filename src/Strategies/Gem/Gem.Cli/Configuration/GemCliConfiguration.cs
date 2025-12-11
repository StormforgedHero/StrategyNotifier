using System.Text.Json.Serialization;

namespace Gem.Cli.Configuration
{
    /// <summary>
    /// Represents the configuration for the GEM CLI application.
    /// Values are typically loaded from gem.cli.json.
    /// </summary>
    public sealed class GemCliConfiguration
    {
        [JsonPropertyName("dataDirectory")]
        public string DataDirectory { get; set; } = string.Empty;

        [JsonPropertyName("usEquityFile")]
        public string UsEquityFile { get; set; } = string.Empty;

        [JsonPropertyName("exUsEquityFile")]
        public string ExUsEquityFile { get; set; } = string.Empty;

        [JsonPropertyName("safeAssetFile")]
        public string SafeAssetFile { get; set; } = string.Empty;

        [JsonPropertyName("outputSignalsFile")]
        public string OutputSignalsFile { get; set; } = string.Empty;

        [JsonPropertyName("lookbackMonths")]
        public int LookbackMonths { get; set; }

        /// <summary>
        /// Validates the configuration values and throws an exception
        /// when any required value is missing or invalid.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DataDirectory))
            {
                throw new InvalidOperationException(
                    "Configuration value 'dataDirectory' must not be null, empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(UsEquityFile))
            {
                throw new InvalidOperationException(
                    "Configuration value 'usEquityFile' must not be null, empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(ExUsEquityFile))
            {
                throw new InvalidOperationException(
                    "Configuration value 'exUsEquityFile' must not be null, empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(SafeAssetFile))
            {
                throw new InvalidOperationException(
                    "Configuration value 'safeAssetFile' must not be null, empty or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(OutputSignalsFile))
            {
                throw new InvalidOperationException(
                    "Configuration value 'outputSignalsFile' must not be null, empty or whitespace.");
            }

            if (LookbackMonths <= 0)
            {
                throw new InvalidOperationException(
                    "Configuration value 'lookbackMonths' must be greater than zero.");
            }
        }
    }
}

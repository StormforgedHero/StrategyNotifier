using System.Text.Json.Serialization;

namespace Gem.Cli.Configuration
{
    /// <summary>
    /// Represents the configuration for the GEM CLI application.
    /// Values are typically loaded from gem.cli.json.
    /// </summary>
    public sealed class GemCliConfiguration
    {
        private const string MessageMustNotBeBlank = "Configuration value '{0}' must not be null, empty or whitespace.";

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
            RequireNotBlank(DataDirectory, "dataDirectory");
            RequireNotBlank(UsEquityFile, "usEquityFile");
            RequireNotBlank(ExUsEquityFile, "exUsEquityFile");
            RequireNotBlank(SafeAssetFile, "safeAssetFile");
            RequireNotBlank(OutputSignalsFile, "outputSignalsFile");

            if (LookbackMonths <= 0)
            {
                throw new InvalidOperationException(
                    "Configuration value 'lookbackMonths' must be greater than zero.");
            }
        }

        private static void RequireNotBlank(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(string.Format(MessageMustNotBeBlank, key));
            }
        }
    }
}

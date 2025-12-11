using System.Text.Json;

namespace Gem.Cli.Configuration
{
    /// <summary>
    /// Loads and validates GEM CLI configuration from a JSON file.
    /// </summary>
    public sealed class GemConfigLoader
    {
        private readonly string _configFilePath;

        public GemConfigLoader(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                throw new ArgumentException(
                    "Configuration file path must not be null, empty or whitespace.",
                    nameof(configFilePath));
            }

            _configFilePath = configFilePath;
        }

        /// <summary>
        /// Loads the configuration from the configured file path.
        /// </summary>
        /// <returns>A validated <see cref="GemCliConfiguration"/> instance.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the configuration file does not exist.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the file is empty, contains invalid JSON or invalid configuration values.
        /// </exception>
        public GemCliConfiguration Load()
        {
            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException(
                    $"Configuration file not found at path '{_configFilePath}'.",
                    _configFilePath);
            }

            string json = File.ReadAllText(_configFilePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"Configuration file '{_configFilePath}' is empty.");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            GemCliConfiguration? configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<GemCliConfiguration>(json, options);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Configuration file '{_configFilePath}' contains invalid JSON.",
                    ex);
            }

            if (configuration is null)
            {
                throw new InvalidOperationException(
                    $"Configuration file '{_configFilePath}' could not be deserialized.");
            }

            configuration.Validate();

            return configuration;
        }
    }
}

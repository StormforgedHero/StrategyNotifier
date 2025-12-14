using Gem.Domain.Model;

namespace Gem.Cli.Configuration
{
    public sealed class GemCliConfiguration
    {
        public GemCliConfiguration(
            PortfolioConfiguration portfolio,
            UpdateSettings update,
            string dataDirectory,
            string outputSignalsFile)
        {
            Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
            Update = update ?? throw new ArgumentNullException(nameof(update));

            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("Data directory must be provided.", nameof(dataDirectory));
            }

            if (string.IsNullOrWhiteSpace(outputSignalsFile))
            {
                throw new ArgumentException("Output file must be provided.", nameof(outputSignalsFile));
            }

            DataDirectory = dataDirectory;
            OutputSignalsFile = outputSignalsFile;
        }

        public PortfolioConfiguration Portfolio { get; }

        public UpdateSettings Update { get; }

        public string DataDirectory { get; private set; }

        public string OutputSignalsFile { get; private set; }

        public void NormalizePaths(string workingDirectory)
        {
            DataDirectory = NormalizePath(DataDirectory, workingDirectory);
            OutputSignalsFile = NormalizePath(OutputSignalsFile, workingDirectory);
        }

        private static string NormalizePath(string value, string workingDirectory)
        {
            if (Path.IsPathRooted(value))
            {
                return Path.GetFullPath(value);
            }

            return Path.GetFullPath(Path.Combine(workingDirectory, value));
        }
    }
}

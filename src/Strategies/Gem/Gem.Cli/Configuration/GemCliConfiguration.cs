using Gem.Domain.Model;

namespace Gem.Cli.Configuration
{
    public sealed class GemCliConfiguration
    {
        public GemCliConfiguration(
            PortfolioConfiguration portfolio,
            UpdateSettings update,
            string storeDirectory,
            string cacheDirectory,
            string outputSignalsFile)
        {
            Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
            Update = update ?? throw new ArgumentNullException(nameof(update));

            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new ArgumentException("Store directory must be provided.", nameof(storeDirectory));
            }

            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new ArgumentException("Cache directory must be provided.", nameof(cacheDirectory));
            }

            if (string.IsNullOrWhiteSpace(outputSignalsFile))
            {
                throw new ArgumentException("Output file must be provided.", nameof(outputSignalsFile));
            }

            StoreDirectory = storeDirectory;
            CacheDirectory = cacheDirectory;
            OutputSignalsFile = outputSignalsFile;
        }

        public PortfolioConfiguration Portfolio { get; }

        public UpdateSettings Update { get; }

        public string StoreDirectory { get; private set; }

        public string CacheDirectory { get; private set; }

        public string OutputSignalsFile { get; private set; }

        public void NormalizePaths(string workingDirectory)
        {
            StoreDirectory = NormalizePath(StoreDirectory, workingDirectory);
            CacheDirectory = NormalizePath(CacheDirectory, workingDirectory);
            OutputSignalsFile = NormalizePath(OutputSignalsFile, workingDirectory);

            if (!Directory.Exists(StoreDirectory))
            {
                throw new FileNotFoundException($"Store directory '{StoreDirectory}' does not exist.", StoreDirectory);
            }

            Directory.CreateDirectory(CacheDirectory);
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

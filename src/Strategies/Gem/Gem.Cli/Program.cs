using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;

namespace Gem.Cli
{
    public static class Program
    {
        public const int ExitCodeSuccess = 0;

        public const int ExitCodeConfigurationFileNotFound = 10;
        public const int ExitCodeConfigurationError = 11;

        public const int ExitCodeInputDataFileNotFound = 20;
        public const int ExitCodeInputDataFormatError = 21;
        public const int ExitCodeInputDataValidationError = 22;

        public const int ExitCodeOutputWriteError = 30;
        public const int ExitCodeIoError = 31;

        public const int ExitCodeUnexpectedError = 99;

        public static int Main(string[] args)
        {
            return Run(
                workingDirectory: Directory.GetCurrentDirectory(),
                output: Console.Out,
                error: Console.Error,
                args: args);
        }

        public static int Run(string workingDirectory, TextWriter output, TextWriter error, string[]? args = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            args ??= Array.Empty<string>();
            bool skipUpdate = args.Contains("--no-update", StringComparer.OrdinalIgnoreCase);
            bool forceUpdate = args.Contains("--force-update", StringComparer.OrdinalIgnoreCase);
            bool updateRequested = args.Contains("--update", StringComparer.OrdinalIgnoreCase);

            if (skipUpdate && forceUpdate)
            {
                error.WriteLine("Invalid arguments: --no-update cannot be combined with --force-update.");
                return ExitCodeConfigurationError;
            }

            if (updateRequested)
            {
                error.WriteLine("Invalid arguments: --update is not supported. Use --force-update, or set update.autoUpdateEnabled=true and run without flags.");
                return ExitCodeConfigurationError;
            }

            string configPath = Path.Combine(workingDirectory, "config", "gem", "gem.config.json");

            GemCliConfiguration configuration;

            try
            {
                var loader = new GemConfigLoader(configPath);
                configuration = loader.Load();
            }
            catch (FileNotFoundException ex)
            {
                error.WriteLine("Configuration file not found.");
                error.WriteLine(ex.Message);
                return ExitCodeConfigurationFileNotFound;
            }
            catch (InvalidOperationException ex)
            {
                error.WriteLine("Configuration error.");
                error.WriteLine(ex.Message);
                return ExitCodeConfigurationError;
            }

            try
            {
                configuration.NormalizePaths(workingDirectory);

                var runner = new GemRunner(configuration);
                IReadOnlyList<Signal> signals = runner.Run(skipUpdate, forceUpdate);

                string updateMode = skipUpdate
                    ? "Disabled (via --no-update)"
                    : forceUpdate
                        ? "Forced (via --force-update)"
                        : configuration.Update.AutoUpdateEnabled
                            ? "Auto (update.autoUpdateEnabled=true)"
                            : "Off (update.autoUpdateEnabled=false)";

                output.WriteLine("StrategyNotifier - GEM CLI");
                output.WriteLine($"Configuration file   : {configPath}");
                output.WriteLine($"Store directory      : {configuration.StoreDirectory}");
                output.WriteLine($"Cache directory      : {configuration.CacheDirectory}");
                output.WriteLine($"Output file          : {configuration.OutputSignalsFile}");
                output.WriteLine($"Momentum window      : {configuration.Portfolio.Momentum.WindowMonths} months");
                output.WriteLine($"Update mode          : {updateMode}");
                output.WriteLine($"Generated signals    : {signals.Count}");

                return ExitCodeSuccess;
            }
            catch (FileNotFoundException ex)
            {
                error.WriteLine("Input data file not found.");
                error.WriteLine(ex.Message);
                return ExitCodeInputDataFileNotFound;
            }
            catch (FormatException ex)
            {
                error.WriteLine("Input data format error.");
                error.WriteLine(ex.Message);
                return ExitCodeInputDataFormatError;
            }
            catch (DomainValidationException ex)
            {
                error.WriteLine("Input data validation error.");
                error.WriteLine(ex.Message);
                return ExitCodeInputDataValidationError;
            }
            catch (GemOutputWriteException ex)
            {
                error.WriteLine("Output write error.");
                error.WriteLine(ex.Message);
                return ExitCodeOutputWriteError;
            }
            catch (UnauthorizedAccessException ex)
            {
                error.WriteLine("I/O error.");
                error.WriteLine(ex.Message);
                return ExitCodeIoError;
            }
            catch (IOException ex)
            {
                error.WriteLine("I/O error.");
                error.WriteLine(ex.Message);
                return ExitCodeIoError;
            }
            catch (Exception ex)
            {
                error.WriteLine("An unexpected error occurred while running the GEM CLI.");
                error.WriteLine(ex.Message);
                return ExitCodeUnexpectedError;
            }
        }
    }
}

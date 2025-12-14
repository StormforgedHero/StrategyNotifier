using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Domain.Exceptions;

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
                error: Console.Error);
        }

        public static int Run(string workingDirectory, TextWriter output, TextWriter error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            string configPath = Path.Combine(workingDirectory, "config", "gem", "gem.cli.json");

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

            NormalizePaths(configuration, workingDirectory);

            try
            {
                var runner = new GemRunner(configuration);
                int signalCount = runner.Run();

                output.WriteLine("StrategyNotifier - GEM CLI");
                output.WriteLine($"Configuration file       : {configPath}");
                output.WriteLine($"Data directory           : {configuration.DataDirectory}");
                output.WriteLine($"Output file              : {configuration.OutputSignalsFile}");
                output.WriteLine($"Lookback window (months) : {configuration.LookbackMonths}");
                output.WriteLine($"Generated signals        : {signalCount}");

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

        private static void NormalizePaths(GemCliConfiguration configuration, string workingDirectory)
        {
            configuration.DataDirectory = NormalizePath(configuration.DataDirectory, workingDirectory);
            configuration.OutputSignalsFile = NormalizePath(configuration.OutputSignalsFile, workingDirectory);
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

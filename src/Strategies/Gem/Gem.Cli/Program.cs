using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Utilities;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using System.Text.Json;

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
            ParsedArguments parsed = ParseArguments(args);

            if (!parsed.IsValid)
            {
                error.WriteLine(parsed.ErrorMessage);
                return ExitCodeConfigurationError;
            }

            if (parsed.SkipUpdate && parsed.ForceUpdate)
            {
                error.WriteLine("Invalid arguments: --no-update cannot be combined with --force-update.");
                return ExitCodeConfigurationError;
            }

            if (parsed.UpdateRequested)
            {
                error.WriteLine("Invalid arguments: --update is not supported. Use --force-update, or set update.autoUpdateEnabled=true and run without flags.");
                return ExitCodeConfigurationError;
            }

            string? configsDirectory = parsed.ConfigsDirectory;
            string? profileId = parsed.ProfileId;

            if (string.IsNullOrWhiteSpace(parsed.ConfigPath) && string.IsNullOrWhiteSpace(configsDirectory) && string.IsNullOrWhiteSpace(profileId))
            {
                string defaultProfilesDir = Path.Combine(workingDirectory, "config", "gem", "profiles");

                if (Directory.Exists(defaultProfilesDir))
                {
                    string[] defaultConfigs = Directory.GetFiles(defaultProfilesDir, "*.profile.json", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(defaultProfilesDir, "*.gem.config.json", SearchOption.AllDirectories))
                        .ToArray();
                    if (defaultConfigs.Length > 0)
                    {
                        configsDirectory = defaultProfilesDir;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(parsed.ConfigPath) && !string.IsNullOrWhiteSpace(configsDirectory)
                || !string.IsNullOrWhiteSpace(parsed.ConfigPath) && !string.IsNullOrWhiteSpace(profileId)
                || !string.IsNullOrWhiteSpace(profileId) && !string.IsNullOrWhiteSpace(configsDirectory))
            {
                error.WriteLine("Invalid arguments: --config, --configs-dir/--profiles-dir, and --profile cannot be combined.");
                return ExitCodeConfigurationError;
            }

            if (!string.IsNullOrWhiteSpace(profileId))
            {
                string profileConfig = Path.Combine("config", "gem", "profiles", $"{profileId}.profile.json");
                string resolved = ResolvePath(profileConfig, workingDirectory);
                var loader = new GemConfigLoader(resolved);
                GemCliConfiguration configuration;
                try
                {
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

                return RunConfiguration(configuration, resolved, parsed, workingDirectory, output, error, out _);
            }

            if (!string.IsNullOrWhiteSpace(configsDirectory))
            {
                return RunBatch(parsed, configsDirectory!, workingDirectory, output, error);
            }

            return RunSingle(parsed, workingDirectory, output, error);
        }

        private static int RunSingle(ParsedArguments parsed, string workingDirectory, TextWriter output, TextWriter error)
        {
            string configPath = ResolvePath(parsed.ConfigPath ?? Path.Combine("config", "gem", "gem.config.json"), workingDirectory);

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

            return RunConfiguration(configuration, configPath, parsed, workingDirectory, output, error, out _);
        }

        private static int RunBatch(ParsedArguments parsed, string configsDirectory, string workingDirectory, TextWriter output, TextWriter error)
        {
            configsDirectory = ResolvePath(configsDirectory, workingDirectory);

            if (!Directory.Exists(configsDirectory))
            {
                error.WriteLine("Configuration directory not found.");
                error.WriteLine(configsDirectory);
                return ExitCodeConfigurationFileNotFound;
            }

            string[] configFiles = Directory.GetFiles(configsDirectory, "*.profile.json", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(configsDirectory, "*.gem.config.json", SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (configFiles.Length == 0)
            {
                error.WriteLine("No configuration files found under the specified directory.");
                return ExitCodeConfigurationError;
            }

            var manifestEntries = new List<ProfilesManifestEntry>();
            string manifestDirectory = Path.GetFullPath(Path.Combine(workingDirectory, "dist", "gem"));

            foreach (string configPath in configFiles)
            {
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

                int exitCode = RunConfiguration(configuration, configPath, parsed, workingDirectory, output, error, out IReadOnlyList<Signal> signals);

                if (exitCode != ExitCodeSuccess)
                {
                    return exitCode;
                }

                manifestEntries.Add(CreateManifestEntry(configsDirectory, configPath, configuration, manifestDirectory, signals));
            }

            WriteManifest(manifestDirectory, manifestEntries);
            return ExitCodeSuccess;
        }

        private static int RunConfiguration(
            GemCliConfiguration configuration,
            string configPath,
            ParsedArguments parsed,
            string workingDirectory,
            TextWriter output,
            TextWriter error,
            out IReadOnlyList<Signal> signals)
        {
            try
            {
                configuration.NormalizePaths(workingDirectory);

                var runner = new GemRunner(configuration);
                signals = runner.Run(parsed.SkipUpdate, parsed.ForceUpdate);

                string updateMode = parsed.SkipUpdate
                    ? "Disabled (via --no-update)"
                    : parsed.ForceUpdate
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
                signals = Array.Empty<Signal>();
                return ExitCodeInputDataFileNotFound;
            }
            catch (FormatException ex)
            {
                error.WriteLine("Input data format error.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeInputDataFormatError;
            }
            catch (DomainValidationException ex)
            {
                error.WriteLine("Input data validation error.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeInputDataValidationError;
            }
            catch (GemOutputWriteException ex)
            {
                error.WriteLine("Output write error.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeOutputWriteError;
            }
            catch (UnauthorizedAccessException ex)
            {
                error.WriteLine("I/O error.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeIoError;
            }
            catch (IOException ex)
            {
                error.WriteLine("I/O error.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeIoError;
            }
            catch (Exception ex)
            {
                error.WriteLine("An unexpected error occurred while running the GEM CLI.");
                error.WriteLine(ex.Message);
                signals = Array.Empty<Signal>();
                return ExitCodeUnexpectedError;
            }
        }

        private static ProfilesManifestEntry CreateManifestEntry(
            string configsDirectory,
            string configPath,
            GemCliConfiguration configuration,
            string manifestDirectory,
            IReadOnlyList<Signal> signals)
        {
            string fileName = Path.GetFileName(configPath);
            string id = fileName
                .Replace(".profile.json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(".gem.config.json", string.Empty, StringComparison.OrdinalIgnoreCase);

            string region = id;
            string title = BuildTitle(region, configuration);

            string signalsPathRelative = Path.GetFileName(configuration.OutputSignalsFile);

            string latestDate = string.Empty;
            bool isRiskOn = false;

            if (signals.Count > 0)
            {
                Signal latest = signals[0];
                latestDate = latest.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                isRiskOn = latest.IsRiskOn;
            }

            return new ProfilesManifestEntry
            {
                Id = id,
                Title = title,
                SignalsPath = signalsPathRelative,
                WindowMonths = configuration.Portfolio.Momentum.WindowMonths,
                LastSignalDate = latestDate,
                IsRiskOn = isRiskOn
            };
        }

        private static string BuildTitle(string region, GemCliConfiguration configuration)
        {
            string prefix = region.ToUpperInvariant();
            string riskOnTickers = string.Join(" / ", configuration.Portfolio.RiskOnInstruments.Select(i => i.Ticker));
            string safe = configuration.Portfolio.RiskOffInstrument.Ticker;
            return $"{prefix} - {riskOnTickers} / {safe}";
        }

        private static void WriteManifest(string manifestDirectory, IReadOnlyList<ProfilesManifestEntry> entries)
        {
            Directory.CreateDirectory(manifestDirectory);
            string manifestPath = Path.Combine(manifestDirectory, "profiles.json");

            var sorted = entries
                .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var payload = new ProfilesManifest
            {
                Profiles = sorted
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(payload, options);
            File.WriteAllText(manifestPath, json + Environment.NewLine, FileEncodings.Utf8NoBom);
        }

        private static string ResolvePath(string path, string workingDirectory)
        {
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            return Path.GetFullPath(Path.Combine(workingDirectory, path));
        }

        private static ParsedArguments ParseArguments(IReadOnlyList<string> args)
        {
            bool skipUpdate = false;
            bool forceUpdate = false;
            bool updateRequested = false;

            bool configMissingValue = false;
            bool configsDirMissingValue = false;
            bool profileMissingValue = false;

            string? configPath = null;
            string? configsDirectory = null;
            string? profileId = null;

            for (int i = 0; i < args.Count; i++)
            {
                string argument = args[i];

                if (string.Equals(argument, "--no-update", StringComparison.OrdinalIgnoreCase))
                {
                    skipUpdate = true;
                    continue;
                }

                if (string.Equals(argument, "--force-update", StringComparison.OrdinalIgnoreCase))
                {
                    forceUpdate = true;
                    continue;
                }

                if (string.Equals(argument, "--update", StringComparison.OrdinalIgnoreCase))
                {
                    updateRequested = true;
                    continue;
                }

                if (argument.StartsWith("--configs-dir", StringComparison.OrdinalIgnoreCase)
                    || argument.StartsWith("--profiles-dir", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryReadValue(argument, args, ref i, out string? value))
                    {
                        configsDirMissingValue = true;
                        continue;
                    }

                    configsDirectory = value;
                    continue;
                }

                if (argument.StartsWith("--profile", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryReadValue(argument, args, ref i, out string? value, treatNextFlagAsMissing: true))
                    {
                        profileMissingValue = true;
                        continue;
                    }

                    profileId = value;
                    continue;
                }

                if (argument.StartsWith("--config", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryReadValue(argument, args, ref i, out string? value, treatNextFlagAsMissing: true))
                    {
                        configMissingValue = true;
                        continue;
                    }

                    configPath = value;
                    continue;
                }
            }

            if (configMissingValue)
            {
                return ParsedArguments.Invalid("Invalid arguments: --config requires a file path. Example: --config config/gem/gem.config.json");
            }

            if (configsDirMissingValue)
            {
                return ParsedArguments.Invalid("Invalid arguments: --configs-dir requires a directory path. Example: --configs-dir config/gem/profiles");
            }

            if (profileMissingValue)
            {
                return ParsedArguments.Invalid("Invalid arguments: --profile requires an id. Example: --profile us");
            }

            return ParsedArguments.Valid(skipUpdate, forceUpdate, updateRequested, configPath, configsDirectory, profileId);
        }

        private static bool TryReadValue(string argument, IReadOnlyList<string> args, ref int index, out string? value, bool treatNextFlagAsMissing = false)
        {
            int equalsIndex = argument.IndexOf('=', StringComparison.Ordinal);

            if (equalsIndex >= 0)
            {
                string inlineValue = argument[(equalsIndex + 1)..].Trim();

                if (string.IsNullOrWhiteSpace(inlineValue))
                {
                    value = null;
                    return false;
                }

                value = inlineValue;
                return true;
            }

            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[index + 1].Trim();
                index++;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                return true;
            }

            value = null;
            return false;
        }
    }
}

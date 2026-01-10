namespace Gem.Cli.Execution
{
    internal sealed class ParsedArguments
    {
        private ParsedArguments(
            bool isValid,
            string? errorMessage,
            bool skipUpdate,
            bool forceUpdate,
            bool updateRequested,
            string? configPath,
            string? configsDirectory,
            string? profileId)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage ?? string.Empty;
            SkipUpdate = skipUpdate;
            ForceUpdate = forceUpdate;
            UpdateRequested = updateRequested;
            ConfigPath = configPath;
            ConfigsDirectory = configsDirectory;
            ProfileId = profileId;
        }

        public bool IsValid { get; }

        public string ErrorMessage { get; }

        public bool SkipUpdate { get; }

        public bool ForceUpdate { get; }

        public bool UpdateRequested { get; }

        public string? ConfigPath { get; }

        public string? ConfigsDirectory { get; }

        public string? ProfileId { get; }

        public static ParsedArguments Invalid(string message) =>
            new ParsedArguments(false, message, false, false, false, null, null, null);

        public static ParsedArguments Valid(
            bool skipUpdate,
            bool forceUpdate,
            bool updateRequested,
            string? configPath,
            string? configsDirectory,
            string? profileId) =>
            new ParsedArguments(true, string.Empty, skipUpdate, forceUpdate, updateRequested, configPath, configsDirectory, profileId);
    }
}

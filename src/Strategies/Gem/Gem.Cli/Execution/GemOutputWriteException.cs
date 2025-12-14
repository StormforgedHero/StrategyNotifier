namespace Gem.Cli.Execution
{
    public sealed class GemOutputWriteException : Exception
    {
        public string OutputPath { get; }

        public GemOutputWriteException(string outputPath, Exception innerException)
            : base($"Failed to write signals output to '{outputPath}'.", innerException)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "Output path must not be null or whitespace.",
                    nameof(outputPath));
            }

            OutputPath = outputPath;
        }
    }
}

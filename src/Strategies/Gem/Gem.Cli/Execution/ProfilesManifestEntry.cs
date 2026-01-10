namespace Gem.Cli.Execution
{
    public sealed class ProfilesManifestEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string SignalsPath { get; set; } = string.Empty;

        public int WindowMonths { get; set; }

        public string LastSignalDate { get; set; } = string.Empty;

        public bool IsRiskOn { get; set; }
    }
}

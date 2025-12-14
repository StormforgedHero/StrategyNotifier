namespace Gem.Cli.Configuration
{
    internal sealed class UpdateConfig
    {
        public bool? EnabledByDefault { get; set; }

        public int? FreshnessDays { get; set; }

        public double? MinHoursBetweenUpdates { get; set; }
    }
}

namespace Gem.Cli.Configuration
{
    internal sealed class UpdateConfig
    {
        public bool? AutoUpdateEnabled { get; set; }

        public int? MaxAgeDays { get; set; }

        public int? MinMinutesBetweenAttempts { get; set; }

        public bool? SaveUpdatedDataToStore { get; set; }
    }
}

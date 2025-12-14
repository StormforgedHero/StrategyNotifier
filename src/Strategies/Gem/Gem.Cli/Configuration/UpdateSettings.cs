namespace Gem.Cli.Configuration
{
    public sealed class UpdateSettings
    {
        public bool Enabled { get; set; } = true;

        public int FreshnessDays { get; set; } = 2;

        public int MinDelaySeconds { get; set; } = 1;

        public void EnsureDefaults()
        {
            if (FreshnessDays <= 0)
            {
                FreshnessDays = 2;
            }

            if (MinDelaySeconds < 0)
            {
                MinDelaySeconds = 0;
            }
        }
    }
}

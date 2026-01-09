namespace Gem.Cli.Configuration
{
    public sealed class UpdateSettings
    {
        public bool AutoUpdateEnabled { get; set; } = false;

        public int MaxAgeDays { get; set; } = 2;

        public int MinMinutesBetweenAttempts { get; set; } = 30;

        public bool SaveUpdatedDataToStore { get; set; } = true;

        public void Validate()
        {
            ValidateGreaterOrEqualZero(MaxAgeDays, nameof(MaxAgeDays));
            ValidateGreaterOrEqualZero(MinMinutesBetweenAttempts, nameof(MinMinutesBetweenAttempts));
        }

        private static void ValidateGreaterOrEqualZero(int value, string name)
        {
            if (value < 0)
            {
                throw new InvalidOperationException($"{name} cannot be negative.");
            }
        }
    }
}

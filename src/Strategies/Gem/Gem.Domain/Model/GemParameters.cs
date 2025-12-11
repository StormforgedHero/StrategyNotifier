namespace Gem.Domain.Model
{
    public sealed class GemParameters
    {
        public int LookbackMonths { get; }

        public static GemParameters Default { get; } = new(12);

        public GemParameters(int lookbackMonths)
        {
            if (lookbackMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lookbackMonths),
                    lookbackMonths,
                    "Lookback months must be greater than zero.");
            }

            LookbackMonths = lookbackMonths;
        }
    }
}

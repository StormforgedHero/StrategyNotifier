using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class Allocation
    {
        public Allocation(Instrument instrument, decimal weight)
        {
            Instrument = instrument ?? throw new DomainValidationException("Allocation requires an instrument.");

            if (weight < 0m || weight > 1m)
            {
                throw new DomainValidationException("Allocation weight must be between 0 and 1.");
            }

            Weight = weight;
        }

        public Instrument Instrument { get; }

        public decimal Weight { get; }
    }
}

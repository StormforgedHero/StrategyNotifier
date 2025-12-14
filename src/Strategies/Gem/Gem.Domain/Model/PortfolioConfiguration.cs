using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class PortfolioConfiguration
    {
        public PortfolioConfiguration(
            IEnumerable<Instrument> riskOnInstruments,
            Instrument riskOffInstrument,
            MomentumParameters momentum)
        {
            ArgumentNullException.ThrowIfNull(riskOnInstruments);
            RiskOnInstruments = riskOnInstruments.ToList();

            if (RiskOnInstruments.Count == 0)
            {
                throw new DomainValidationException("At least one risk-on instrument must be provided.");
            }

            RiskOffInstrument = riskOffInstrument ?? throw new DomainValidationException("Risk-off instrument must be provided.");
            Momentum = momentum ?? throw new DomainValidationException("Momentum parameters must be provided.");
        }

        public IReadOnlyList<Instrument> RiskOnInstruments { get; }

        public Instrument RiskOffInstrument { get; }

        public MomentumParameters Momentum { get; }
    }
}

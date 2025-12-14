using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class Signal
    {
        public Signal(
            DateOnly date,
            int windowMonths,
            bool isRiskOn,
            IEnumerable<Allocation> allocations,
            decimal absoluteReturn,
            int relativeRank,
            string comment)
        {
            if (windowMonths <= 0)
            {
                throw new DomainValidationException("Window months must be positive.");
            }

            Date = date;
            WindowMonths = windowMonths;
            IsRiskOn = isRiskOn;
            Allocations = allocations?.ToList() ?? throw new DomainValidationException("Allocations are required.");

            if (Allocations.Count == 0)
            {
                throw new DomainValidationException("At least one allocation is required.");
            }

            AbsoluteReturn = absoluteReturn;
            RelativeRank = relativeRank;
            Comment = comment ?? string.Empty;
        }

        public DateOnly Date { get; }

        public int WindowMonths { get; }

        public bool IsRiskOn { get; }

        public IReadOnlyList<Allocation> Allocations { get; }

        public decimal AbsoluteReturn { get; }

        public int RelativeRank { get; }

        public string Comment { get; }
    }
}

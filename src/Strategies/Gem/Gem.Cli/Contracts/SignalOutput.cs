using System.Text.Json.Serialization;

namespace Gem.Cli.Contracts
{
    public sealed class SignalOutput
    {
        public string Date { get; init; } = string.Empty;

        public int WindowMonths { get; init; }

        public bool IsRiskOn { get; init; }

        public IReadOnlyList<AllocationOutput> Allocations { get; init; } = Array.Empty<AllocationOutput>();

        public decimal AbsoluteReturn { get; init; }

        public int RelativeRank { get; init; }

        public string Comment { get; init; } = string.Empty;
    }
}

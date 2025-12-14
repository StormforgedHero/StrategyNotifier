namespace Gem.Cli.Contracts
{
    public sealed class AllocationOutput
    {
        public string Ticker { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public decimal Weight { get; init; }
    }
}

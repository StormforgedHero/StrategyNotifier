namespace Gem.Domain.Model
{
    public sealed class PricePoint
    {
        public PricePoint(DateOnly date, decimal close)
        {
            Date = date;
            Close = close;
        }

        public DateOnly Date { get; }

        public decimal Close { get; }
    }
}
